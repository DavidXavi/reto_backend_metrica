using BuildingBlocks.Core;
using BuildingBlocks.Infrastructure.Mensajeria;
using Contracts;
using Serilog.Context;

namespace Notificaciones.Worker;

public sealed class ConsumidorNotificaciones(
    RabbitMqConexion conexion, IServiceScopeFactory scopes, ILogger<ConsumidorNotificaciones> log)
    : ConsumidorRabbit<CargaFinalizada>(conexion, scopes, log)
{
    protected override string Cola => Rutas.ColaNotificaciones;

    protected override async Task ProcesarAsync(CargaFinalizada m, string? correlationId,
        IServiceProvider sp, CancellationToken ct)
    {
        using var _ = LogContext.PushProperty("IdCarga", m.IdCarga);
        await sp.GetRequiredService<IHandler<CargaFinalizada, bool>>().HandleAsync(m, ct);
    }
}