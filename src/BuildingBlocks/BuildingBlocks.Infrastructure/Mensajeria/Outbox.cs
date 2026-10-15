using System.Data;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BuildingBlocks.Infrastructure.Mensajeria;

// Patrón outbox: el mensaje se guarda en la misma transacción que los datos.
// Si RabbitMQ está caído, el mensaje espera en la tabla y sale después.
public static class Outbox
{
    public static Task AgregarAsync(IDbConnection conn, IDbTransaction tx, string esquema,
        string routingKey, object mensaje, string? correlationId) =>
        conn.ExecuteAsync(
            $"INSERT INTO {esquema}.outbox (routing_key, payload, correlation_id) " +
            "VALUES (@routingKey, @payload::jsonb, @correlationId)",
            new { routingKey, payload = JsonSerializer.Serialize(mensaje, Json.Opciones), correlationId },
            tx);

    public static IServiceCollection AgregarMensajeria(this IServiceCollection s, IConfiguration c,
        string? esquemaOutbox = null)
    {
        s.Configure<RabbitMqOptions>(c.GetSection("RabbitMq"));
        s.AddSingleton<RabbitMqConexion>();
        s.AddSingleton<PublicadorRabbit>();
        if (esquemaOutbox is not null)
        {
            s.AddSingleton(new OutboxOptions(esquemaOutbox));
            s.AddHostedService<OutboxPublicador>();
        }
        return s;
    }
}

public sealed record OutboxOptions(string Esquema);

public sealed class OutboxPublicador(
    NpgsqlDataSource db, PublicadorRabbit publicador, OutboxOptions opciones,
    ILogger<OutboxPublicador> log) : BackgroundService
{
    private sealed record Pendiente(long Id, string RoutingKey, string Payload, string? CorrelationId);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            try
            {
                await PublicarPendientesAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "No se pudo publicar el outbox, se reintenta en el siguiente ciclo");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task PublicarPendientesAsync(CancellationToken ct)
    {
        var esquema = opciones.Esquema;
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // SKIP LOCKED: si hay dos réplicas del servicio, cada una toma filas distintas
        var pendientes = await conn.QueryAsync<Pendiente>(
            $"""
            SELECT id, routing_key AS RoutingKey, payload::text AS Payload, correlation_id AS CorrelationId
            FROM {esquema}.outbox
            WHERE publicado IS NULL
            ORDER BY id
            LIMIT 50
            FOR UPDATE SKIP LOCKED
            """, transaction: tx);

        foreach (var m in pendientes)
        {
            await publicador.PublicarAsync(m.RoutingKey, Encoding.UTF8.GetBytes(m.Payload),
                $"{esquema}-{m.Id}", m.CorrelationId, ct);
            await conn.ExecuteAsync($"UPDATE {esquema}.outbox SET publicado = now() WHERE id = @Id",
                new { m.Id }, tx);
            log.LogInformation("Publicado {RoutingKey} desde el outbox {Id}", m.RoutingKey, m.Id);
        }

        await tx.CommitAsync(ct);
    }
}