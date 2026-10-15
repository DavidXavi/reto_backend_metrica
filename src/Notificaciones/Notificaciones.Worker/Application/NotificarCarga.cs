using BuildingBlocks.Core;
using Contracts;

namespace Notificaciones.Worker.Application;

public interface IEnviadorCorreo
{
    Task EnviarAsync(string para, string asunto, string html, CancellationToken ct);
}

public interface ICargaEstados
{
    Task<string?> EstadoAsync(int idCarga, CancellationToken ct);
    Task MarcarNotificadoAsync(int idCarga, CancellationToken ct);
}

public sealed class NotificarCargaHandler(
    ICargaEstados estados, IEnviadorCorreo correo, ILogger<NotificarCargaHandler> log)
    : IHandler<CargaFinalizada, bool>
{
    public async Task<bool> HandleAsync(CargaFinalizada m, CancellationToken ct)
    {
        // Idempotencia: solo se notifica una carga Finalizado
        if (await estados.EstadoAsync(m.IdCarga, ct) != "Finalizado")
        {
            log.LogInformation("Carga {IdCarga} ya notificada o inexistente, se ignora", m.IdCarga);
            return false;
        }

        await correo.EnviarAsync(m.Usuario, $"Carga {m.IdCarga} finalizada: {m.Resultado}", Cuerpo(m), ct);
        await estados.MarcarNotificadoAsync(m.IdCarga, ct);
        log.LogInformation("Carga {IdCarga} notificada a {Usuario}", m.IdCarga, m.Usuario);
        return true;
    }

    private static string Cuerpo(CargaFinalizada m) => $"""
        <h2>Tu carga {m.IdCarga} terminó</h2>
        <p>Resultado: <b>{m.Resultado}</b></p>
        <ul>
          <li>Filas leídas: {m.TotalFilas}</li>
          <li>Insertadas: {m.Insertadas}</li>
          <li>Existentes: {m.Existentes}</li>
          <li>Inválidas: {m.Invalidas}</li>
        </ul>
        <p>Fecha de fin: {m.FechaFin:yyyy-MM-dd HH:mm:ss} UTC</p>
        """;
}