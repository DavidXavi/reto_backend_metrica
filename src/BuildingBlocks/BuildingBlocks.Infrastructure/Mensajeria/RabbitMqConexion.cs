using System.Text.Json;
using Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BuildingBlocks.Infrastructure.Mensajeria;

public sealed class RabbitMqOptions
{
    public string Host { get; init; } = "localhost";
    public string Usuario { get; init; } = "guest";
    public string Clave { get; init; } = "guest";
}

public static class Json
{
    // camelCase, igual que los ejemplos del enunciado: { "idCarga": 123, ... }
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);
}

// Una conexión TCP por proceso (es cara de abrir). Los canales sí se crean por uso.
public sealed class RabbitMqConexion(IOptions<RabbitMqOptions> opciones, ILogger<RabbitMqConexion> log)
    : IAsyncDisposable
{
    private readonly SemaphoreSlim _candado = new(1, 1);
    private IConnection? _conexion;

    public async Task<IConnection> ObtenerAsync(CancellationToken ct)
    {
        if (_conexion is { IsOpen: true }) return _conexion;

        await _candado.WaitAsync(ct);
        try
        {
            if (_conexion is { IsOpen: true }) return _conexion;

            var fabrica = new ConnectionFactory
            {
                HostName = opciones.Value.Host,
                UserName = opciones.Value.Usuario,
                Password = opciones.Value.Clave,
                AutomaticRecoveryEnabled = true        // se reconecta solo si RabbitMQ se reinicia
            };

            for (var intento = 1; ; intento++)
            {
                try
                {
                    _conexion = await fabrica.CreateConnectionAsync(ct);
                    return _conexion;
                }
                catch (Exception ex) when (intento < 10)
                {
                    log.LogWarning(ex, "RabbitMQ no responde, intento {Intento}", intento);
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
                }
            }
        }
        finally
        {
            _candado.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexion is not null) await _conexion.DisposeAsync();
    }
}

public static class Topologia
{
    // Declarar es idempotente: cada servicio lo hace al arrancar y no importa quién llega primero.
    public static async Task DeclararAsync(IChannel canal, CancellationToken ct)
    {
        await canal.ExchangeDeclareAsync(Rutas.Exchange, ExchangeType.Direct, durable: true, cancellationToken: ct);
        await canal.ExchangeDeclareAsync(Rutas.ExchangeDlx, ExchangeType.Direct, durable: true, cancellationToken: ct);

        (string Cola, string RoutingKey)[] colas =
        [
            (Rutas.ColaCargaMasiva, Rutas.CargaSolicitada),
            (Rutas.ColaNotificaciones, Rutas.CargaFinalizada)
        ];

        foreach (var (cola, routingKey) in colas)
        {
            var dlq = cola + ".dlq";
            await canal.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
            await canal.QueueBindAsync(dlq, Rutas.ExchangeDlx, dlq, cancellationToken: ct);

            await canal.QueueDeclareAsync(cola, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = Rutas.ExchangeDlx,
                    ["x-dead-letter-routing-key"] = dlq
                },
                cancellationToken: ct);
            await canal.QueueBindAsync(cola, Rutas.Exchange, routingKey, cancellationToken: ct);
        }
    }
}