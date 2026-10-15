using BuildingBlocks.Core;
using BuildingBlocks.Infrastructure.Mensajeria;
using CargaMasiva.Application;
using Contracts;
using Serilog.Context;

namespace CargaMasiva.Worker;

public sealed class ConsumidorCargaMasiva(
    RabbitMqConexion conexion, IServiceScopeFactory scopes, ILogger<ConsumidorCargaMasiva> log)
    : ConsumidorRabbit<CargaSolicitada>(conexion, scopes, log)
{
    protected override string Cola => Rutas.ColaCargaMasiva;

    protected override async Task ProcesarAsync(CargaSolicitada m, string? correlationId,
        IServiceProvider sp, CancellationToken ct)
    {
        using var _ = LogContext.PushProperty("IdCarga", m.IdCarga);
        var handler = sp.GetRequiredService<IHandler<ProcesarCargaCommand, bool>>();
        await handler.HandleAsync(new ProcesarCargaCommand(m, correlationId), ct);
    }

    // Se agotaron los reintentos: la carga se cierra con Error y el usuario igual recibe su correo
    protected override Task AlFallarAsync(CargaSolicitada m, Exception ex, string? correlationId, IServiceProvider sp) =>
        sp.GetRequiredService<ICargaRepositorio>()
          .CerrarConErrorAsync(m.IdCarga, m.Usuario, ex.Message, correlationId, CancellationToken.None);
}