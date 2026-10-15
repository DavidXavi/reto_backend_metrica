using BuildingBlocks.Infrastructure.Mensajeria;
using CargaMasiva.Application;
using CargaMasiva.Domain;
using Contracts;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace CargaMasiva.Infrastructure;

public sealed class CargaRepositorio(NpgsqlDataSource db) : ICargaRepositorio
{
    private const string Servicio = "CargaMasiva";

    public async Task<bool> IniciarAsync(int idCarga, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        // EnProceso también vale: el worker pudo caerse a la mitad y el mensaje volvió.
        // Como todo se guarda en una sola transacción, no quedó nada a medias.
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT control.fn_cambiar_estado(@idCarga, 'EnProceso', @Servicio)
                OR EXISTS (SELECT 1 FROM control.carga_archivo WHERE id = @idCarga AND estado = 'EnProceso')
            """, new { idCarga, Servicio });
    }

    public async Task<ResumenCarga> GuardarYCerrarAsync(int idCarga, string usuario,
        IReadOnlyList<FilaProducto> validas, IReadOnlyList<FilaInvalida> invalidas,
        string? correlationId, CancellationToken ct)
    {
        try
        {
            return await GuardarAsync(idCarga, usuario, validas, invalidas, correlationId, ct);
        }
        catch (PostgresException ex) when (ex is { SqlState: PostgresErrorCodes.UniqueViolation, TableName: "carga_periodo" })
        {
            // Otra carga tomó el mismo mes en paralelo. La transacción ya se deshizo.
            return await CerrarBloqueadaAsync(idCarga, usuario, validas, invalidas, correlationId, ct);
        }
    }

    private async Task<ResumenCarga> GuardarAsync(int idCarga, string usuario,
        IReadOnlyList<FilaProducto> validas, IReadOnlyList<FilaInvalida> invalidas,
        string? correlationId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await AuditarInvalidasAsync(conn, tx, idCarga, invalidas);

        var resumen = new ResumenCarga(Resultados.Rechazado, invalidas.Count, 0, 0, invalidas.Count);
        if (validas.Count > 0)
        {
            await CopiarAStagingAsync(conn, idCarga, validas, ct);

            var r = await conn.QuerySingleAsync<(string Resultado, int Insertadas, int Existentes)>(
                "SELECT resultado, insertadas, existentes FROM carga.fn_procesar_carga(@idCarga)",
                new { idCarga }, tx);

            resumen = new ResumenCarga(
                Resultados.Decidir(r.Resultado, validas.Count, invalidas.Count),
                validas.Count + invalidas.Count, r.Insertadas, r.Existentes, invalidas.Count);

            if (r.Insertadas > 0)
                await conn.ExecuteAsync("SELECT control.fn_cambiar_estado(@idCarga, 'Cargado', @Servicio)",
                    new { idCarga, Servicio }, tx);
        }

        await CerrarAsync(conn, tx, idCarga, usuario, resumen, null, correlationId);
        await tx.CommitAsync(ct);
        return resumen;
    }

    private async Task<ResumenCarga> CerrarBloqueadaAsync(int idCarga, string usuario,
        IReadOnlyList<FilaProducto> validas, IReadOnlyList<FilaInvalida> invalidas,
        string? correlationId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await AuditarInvalidasAsync(conn, tx, idCarga, invalidas);
        await conn.ExecuteAsync(
            """
            INSERT INTO carga.auditoria_fallo (carga_id, periodo, motivo, detalle)
            VALUES (@idCarga, @periodo, 'PeriodoBloqueado', 'Otra carga del mismo periodo está en proceso')
            """,
            validas.Select(v => v.Periodo).Distinct().Select(periodo => new { idCarga, periodo }), tx);

        var resumen = new ResumenCarga(Resultados.Rechazado, validas.Count + invalidas.Count, 0, 0, invalidas.Count);
        await CerrarAsync(conn, tx, idCarga, usuario, resumen, "Periodo bloqueado por una carga simultánea", correlationId);
        await tx.CommitAsync(ct);
        return resumen;
    }

    public async Task CerrarConErrorAsync(int idCarga, string usuario, string detalle, string? correlationId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await CerrarAsync(conn, tx, idCarga, usuario, new ResumenCarga(Resultados.Error, 0, 0, 0, 0),
            detalle.Length > 500 ? detalle[..500] : detalle, correlationId);
        await tx.CommitAsync(ct);
    }

    // Finalizado + mensaje de notificación en el outbox, en la misma transacción
    private static async Task CerrarAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int idCarga,
        string usuario, ResumenCarga r, string? detalle, string? correlationId)
    {
        var fin = await conn.ExecuteScalarAsync<DateTime?>(
            """
            SELECT control.fn_finalizar_carga(@idCarga, @Resultado, @Total, @Insertadas,
                                              @Existentes, @Invalidas, @Servicio, @detalle)
            """,
            new { idCarga, r.Resultado, r.Total, r.Insertadas, r.Existentes, r.Invalidas, Servicio, detalle }, tx);

        if (fin is null) return;   // ya estaba cerrada: no se notifica dos veces

        await Outbox.AgregarAsync(conn, tx, "carga", Rutas.CargaFinalizada,
            new CargaFinalizada(idCarga, usuario, fin.Value, r.Resultado, r.Total, r.Insertadas, r.Existentes, r.Invalidas),
            correlationId);
    }

    private static Task AuditarInvalidasAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int idCarga,
        IReadOnlyList<FilaInvalida> invalidas) =>
        conn.ExecuteAsync(
            """
            INSERT INTO carga.auditoria_fallo (carga_id, fila_excel, periodo, codigo_producto, motivo, detalle)
            VALUES (@idCarga, @Fila, @Periodo, @Codigo, 'FilaInvalida', @Motivo)
            """,
            invalidas.Select(i => new { idCarga, i.Fila, i.Periodo, i.Codigo, i.Motivo }), tx);

    // COPY binario: la forma más rápida de meter muchas filas en PostgreSQL.
    // Usa la transacción abierta de la conexión.
    private static async Task CopiarAStagingAsync(NpgsqlConnection conn, int idCarga,
        IReadOnlyList<FilaProducto> filas, CancellationToken ct)
    {
        await using var w = await conn.BeginBinaryImportAsync(
            "COPY carga.staging_producto (carga_id, fila_excel, periodo, codigo_producto, nombre_producto, precio) " +
            "FROM STDIN (FORMAT BINARY)", ct);

        foreach (var f in filas)
        {
            await w.StartRowAsync(ct);
            await w.WriteAsync(idCarga, NpgsqlDbType.Integer, ct);
            await w.WriteAsync(f.Fila, NpgsqlDbType.Integer, ct);
            await w.WriteAsync(f.Periodo, NpgsqlDbType.Char, ct);
            await w.WriteAsync(f.Codigo, NpgsqlDbType.Varchar, ct);
            await w.WriteAsync(f.Nombre, NpgsqlDbType.Varchar, ct);
            await w.WriteAsync(f.Precio, NpgsqlDbType.Numeric, ct);
        }
        await w.CompleteAsync(ct);
    }
}

public sealed class DatosConsultas(NpgsqlDataSource db) : IDatosConsultas
{
    // Solo el dueño de la carga ve sus datos
    private const string EsDelUsuario =
        "EXISTS (SELECT 1 FROM control.carga_archivo c WHERE c.id = @idCarga AND c.usuario = @usuario)";

    public async Task<IReadOnlyList<RegistroDto>> RegistrosAsync(int idCarga, string usuario, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return (await conn.QueryAsync<RegistroDto>(
            $"""
            SELECT fila_excel, periodo, codigo_producto, nombre_producto, precio
            FROM carga.producto
            WHERE carga_id = @idCarga AND {EsDelUsuario}
            ORDER BY fila_excel
            """, new { idCarga, usuario })).AsList();
    }

    public async Task<IReadOnlyList<FalloDto>> FallosAsync(int idCarga, string usuario, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return (await conn.QueryAsync<FalloDto>(
            $"""
            SELECT fila_excel, periodo, codigo_producto, motivo, detalle
            FROM carga.auditoria_fallo
            WHERE carga_id = @idCarga AND {EsDelUsuario}
            ORDER BY fila_excel NULLS FIRST, id
            """, new { idCarga, usuario })).AsList();
    }
}