using Dapper;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Notificaciones.Worker.Application;
using Npgsql;

namespace Notificaciones.Worker.Infrastructure;

// Todo configurable por variables de entorno: Smtp__Host, Smtp__Puerto, Smtp__Usuario...
public sealed class SmtpOptions
{
    public string Host { get; init; } = "localhost";
    public int Puerto { get; init; } = 1025;
    public bool UsarTls { get; init; }
    public string? Usuario { get; init; }
    public string? Clave { get; init; }
    public string Remitente { get; init; } = "no-responder@reto.pe";
}

public sealed class EnviadorCorreo(IOptions<SmtpOptions> opciones) : IEnviadorCorreo
{
    public async Task EnviarAsync(string para, string asunto, string html, CancellationToken ct)
    {
        var o = opciones.Value;
        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(o.Remitente));
        msg.To.Add(MailboxAddress.Parse(para));
        msg.Subject = asunto;
        msg.Body = new TextPart("html") { Text = html };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(o.Host, o.Puerto,
            o.UsarTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
        if (!string.IsNullOrEmpty(o.Usuario))
            await smtp.AuthenticateAsync(o.Usuario, o.Clave ?? "", ct);
        await smtp.SendAsync(msg, ct);
        await smtp.DisconnectAsync(true, ct);
    }
}

public sealed class CargaEstados(NpgsqlDataSource db) : ICargaEstados
{
    public async Task<string?> EstadoAsync(int idCarga, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<string?>(
            "SELECT estado FROM control.carga_archivo WHERE id = @idCarga", new { idCarga });
    }

    public async Task MarcarNotificadoAsync(int idCarga, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await conn.ExecuteScalarAsync<bool>(
            "SELECT control.fn_cambiar_estado(@idCarga, 'Notificado', 'Notificaciones')", new { idCarga });
    }
}