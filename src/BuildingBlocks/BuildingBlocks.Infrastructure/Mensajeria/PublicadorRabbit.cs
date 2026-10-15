using Contracts;
using RabbitMQ.Client;

namespace BuildingBlocks.Infrastructure.Mensajeria;

// Publica con confirmación del broker: BasicPublishAsync no termina hasta que
// RabbitMQ dice "lo guardé". Si no puede enrutarlo (mandatory), lanza excepción.
public sealed class PublicadorRabbit(RabbitMqConexion conexion) : IAsyncDisposable
{
    private readonly SemaphoreSlim _candado = new(1, 1);   // un canal no se comparte entre hilos
    private IChannel? _canal;

    public async Task PublicarAsync(string routingKey, ReadOnlyMemory<byte> cuerpo,
        string messageId, string? correlationId, CancellationToken ct)
    {
        await _candado.WaitAsync(ct);
        try
        {
            if (_canal is not { IsOpen: true })
            {
                var conn = await conexion.ObtenerAsync(ct);
                _canal = await conn.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true,
                                             publisherConfirmationTrackingEnabled: true), ct);
                await Topologia.DeclararAsync(_canal, ct);
            }

            var props = new BasicProperties
            {
                Persistent = true,                 // sobrevive a un reinicio de RabbitMQ
                ContentType = "application/json",
                MessageId = messageId,
                CorrelationId = correlationId
            };
            await _canal.BasicPublishAsync(Rutas.Exchange, routingKey, mandatory: true,
                basicProperties: props, body: cuerpo, cancellationToken: ct);
        }
        finally
        {
            _candado.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_canal is not null) await _canal.DisposeAsync();
    }
}