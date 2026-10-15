using BuildingBlocks.Infrastructure.Mensajeria;
using Contracts;
using Control.Application;
using Control.Domain;
using Dapper;
using Npgsql;

namespace Control.Infrastructure;

public sealed class CargaRepositorio(NpgsqlDataSource db) : ICargaRepositorio
{
    public async Task<int> RegistrarPendienteAsync(NuevaCarga c, string? correlationId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var id = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO control.carga_archivo (nombre_archivo, ruta_archivo, tamano_bytes, usuario, ip_origen, estado)
            VALUES (@NombreArchivo, @RutaArchivo, @TamanoBytes, @Usuario, @Ip, @Estado)
            RETURNING id
            """,
            new { c.NombreArchivo, c.RutaArchivo, c.TamanoBytes, c.Usuario, c.Ip, Estado = EstadoCarga.Pendiente }, tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO control.carga_estado_historial (carga_id, estado, servicio, detalle)
            VALUES (@id, @Estado, 'Control', @Detalle)
            """,
            new { id, Estado = EstadoCarga.Pendiente, Detalle = $"Subido por {c.Usuario} desde {c.Ip}" }, tx);

        // Mismo commit que la carga: o se guardan los dos o ninguno
        await Outbox.AgregarAsync(conn, tx, "control", Rutas.CargaSolicitada,
            new CargaSolicitada(id, c.RutaArchivo, c.Usuario), correlationId);

        await tx.CommitAsync(ct);
        return id;
    }
}

public sealed class CargaConsultas(NpgsqlDataSource db) : ICargaConsultas
{
    public async Task<Pagina<CargaResumen>> ListarAsync(string usuario, int pagina, int tamano, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var items = await conn.QueryAsync<CargaResumen>(
            """
            SELECT id, nombre_archivo, estado, resultado, fecha_registro, fecha_fin
            FROM control.carga_archivo
            WHERE usuario = @usuario
            ORDER BY id DESC
            LIMIT @tamano OFFSET @salto
            """, new { usuario, tamano, salto = (pagina - 1) * tamano });

        var total = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM control.carga_archivo WHERE usuario = @usuario", new { usuario });

        return new Pagina<CargaResumen>(items.AsList(), pagina, tamano, total);
    }

    public async Task<CargaDetalle?> ObtenerAsync(int id, string usuario, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var carga = await conn.QuerySingleOrDefaultAsync<CargaDetalle>(
            """
            SELECT id, nombre_archivo, ruta_archivo, tamano_bytes, usuario, estado, resultado,
                   total_filas, insertadas, existentes, invalidas, fecha_registro, fecha_fin
            FROM control.carga_archivo
            WHERE id = @id AND usuario = @usuario
            """, new { id, usuario });
        if (carga is null) return null;

        var historial = await conn.QueryAsync<EstadoHistorial>(
            """
            SELECT estado, servicio, detalle, fecha
            FROM control.carga_estado_historial
            WHERE carga_id = @id
            ORDER BY id
            """, new { id });

        return carga with { Historial = historial.AsList() };
    }
}