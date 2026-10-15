using System.Text.Json;
using BuildingBlocks.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace BuildingBlocks.Infrastructure.Mensajeria;

// Base de todos los consumidores: ack manual, prefetch 1, reintentos con Polly
// y, si igual falla, el mensaje va a la DLQ. Cada servicio solo implementa ProcesarAsync.
public abstract class ConsumidorRabbit<TMensaje>(
    RabbitMqConexion conexion, IServiceScopeFactory scopes, ILogger logger) : BackgroundService
{
    protected abstract string Cola { get; }

    protected abstract Task ProcesarAsync(TMensaje mensaje, string? correlationId, IServiceProvider sp, CancellationToken ct);

    // Se llama cuando se agotaron los reintentos, antes de mandar a la DLQ.
    protected virtual Task AlFallarAsync(TMensaje mensaje, Exception ex, string? correlationId, IServiceProvider sp) =>
        Task.CompletedTask;

    private readonly ResiliencePipeline _reintentos = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,          // 2 s, 4 s, 8 s
            ShouldHandle = new PredicateBuilder().Handle<Exception>(e => e is not ValidacionException)
        })
        .Build();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var conn = await conexion.ObtenerAsync(stoppingToken);
        var canal = await conn.CreateChannelAsync(cancellationToken: stoppingToken);
        await Topologia.DeclararAsync(canal, stoppingToken);
        await canal.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, stoppingToken);

        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.ReceivedAsync += async (_, ea) =>
        {
            var correlationId = ea.BasicProperties.CorrelationId;
            using var contexto = LogContext.PushProperty("CorrelationId", correlationId);
            using var scope = scopes.CreateScope();       // un scope por mensaje, como una petición HTTP
            TMensaje? mensaje = default;
            try
            {
                mensaje = JsonSerializer.Deserialize<TMensaje>(ea.Body.Span, Json.Opciones)
                    ?? throw new ValidacionException("Mensaje vacío");

                await _reintentos.ExecuteAsync(
                    async ct => await ProcesarAsync(mensaje, correlationId, scope.ServiceProvider, ct), stoppingToken);

                await canal.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // el servicio se está apagando: devolvemos el mensaje a la cola, no a la DLQ
                await canal.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Mensaje {MessageId} de {Cola} enviado a la DLQ",
                    ea.BasicProperties.MessageId, Cola);

                if (mensaje is not null)
                {
                    try { await AlFallarAsync(mensaje, ex, correlationId, scope.ServiceProvider); }
                    catch (Exception ex2) { logger.LogError(ex2, "Falló el manejo del error"); }
                }
                await canal.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await canal.BasicConsumeAsync(Cola, autoAck: false, consumer: consumidor, cancellationToken: stoppingToken);
        logger.LogInformation("Escuchando la cola {Cola}", Cola);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}