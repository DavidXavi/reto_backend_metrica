using BuildingBlocks.Core;
using CargaMasiva.Domain;
using Contracts;
using Microsoft.Extensions.Logging;

namespace CargaMasiva.Application;

public sealed record ProcesarCargaCommand(CargaSolicitada Mensaje, string? CorrelationId);

public sealed class ProcesarCargaHandler(
    ICargaRepositorio repo, IAlmacenArchivos almacen, ILectorExcel lector, ILogger<ProcesarCargaHandler> log)
    : IHandler<ProcesarCargaCommand, bool>
{
    public async Task<bool> HandleAsync(ProcesarCargaCommand cmd, CancellationToken ct)
    {
        var m = cmd.Mensaje;

        // Idempotencia: si la carga ya avanzó, este mensaje es un duplicado
        if (!await repo.IniciarAsync(m.IdCarga, ct))
        {
            log.LogInformation("Carga {IdCarga} ya procesada, se ignora el mensaje", m.IdCarga);
            return false;
        }

        var archivo = await almacen.DescargarAsync(m.RutaArchivo, ct);

        var validas = new List<FilaProducto>();
        var invalidas = new List<FilaInvalida>();
        var omitidas = 0;
        foreach (var f in lector.Leer(archivo))
        {
            var r = LimpiadorFila.Limpiar(f.Fila, f.Periodo, f.Codigo, f.Nombre, f.Precio);
            switch (r.Tipo)
            {
                case TipoFila.Valida: validas.Add(r.Valida!); break;
                case TipoFila.Invalida: invalidas.Add(r.Invalida!); break;
                default: omitidas++; break;
            }
        }

        var resumen = await repo.GuardarYCerrarAsync(m.IdCarga, m.Usuario, validas, invalidas, cmd.CorrelationId, ct);

        log.LogInformation(
            "Carga {IdCarga} {Resultado}: {Insertadas} insertadas, {Existentes} existentes, {Invalidas} inválidas, {Omitidas} filas vacías",
            m.IdCarga, resumen.Resultado, resumen.Insertadas, resumen.Existentes, resumen.Invalidas, omitidas);
        return true;
    }
}