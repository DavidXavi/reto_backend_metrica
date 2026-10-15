namespace Contracts;

// Lo que viaja por RabbitMQ. Es el contrato entre servicios: si lo cambias,
// cambias a todos los que lo consumen.
public sealed record CargaSolicitada(int IdCarga, string RutaArchivo, string Usuario);

public sealed record CargaFinalizada(
    int IdCarga, string Usuario, DateTime FechaFin, string Resultado,
    int TotalFilas, int Insertadas, int Existentes, int Invalidas);

public static class Rutas
{
    public const string Exchange = "cargas";
    public const string ExchangeDlx = "cargas.dlx";

    public const string CargaSolicitada = "carga.solicitada";
    public const string CargaFinalizada = "carga.finalizada";

    public const string ColaCargaMasiva = "carga_masiva";
    public const string ColaNotificaciones = "notificaciones";
}