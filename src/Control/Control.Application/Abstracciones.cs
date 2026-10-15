using Control.Domain;

namespace Control.Application;

// La capa Application define lo que necesita. Infrastructure lo implementa.
// Así el caso de uso no sabe si el archivo va a SeaweedFS, S3 o disco.
public interface IAlmacenArchivos
{
    Task SubirAsync(string ruta, byte[] contenido, CancellationToken ct);
    Task<byte[]> DescargarAsync(string ruta, CancellationToken ct);
}

public interface ICargaRepositorio
{
    // Guarda la carga Pendiente, su historial y el mensaje del outbox en una transacción
    Task<int> RegistrarPendienteAsync(NuevaCarga carga, string? correlationId, CancellationToken ct);
}

public interface ICargaConsultas
{
    Task<Pagina<CargaResumen>> ListarAsync(string usuario, int pagina, int tamano, CancellationToken ct);
    Task<CargaDetalle?> ObtenerAsync(int id, string usuario, CancellationToken ct);
}

public sealed class CargaOptions
{
    public long TamanoMaximoMb { get; init; } = 10;
}

public sealed record Pagina<T>(IReadOnlyList<T> Items, int PaginaActual, int Tamano, long Total);

public sealed record CargaResumen(
    int Id, string NombreArchivo, string Estado, string? Resultado,
    DateTime FechaRegistro, DateTime? FechaFin);

public sealed record CargaDetalle(
    int Id, string NombreArchivo, string RutaArchivo, long TamanoBytes, string Usuario,
    string Estado, string? Resultado, int? TotalFilas, int? Insertadas, int? Existentes, int? Invalidas,
    DateTime FechaRegistro, DateTime? FechaFin)
{
    public IReadOnlyList<EstadoHistorial> Historial { get; init; } = [];
}

public sealed record EstadoHistorial(string Estado, string Servicio, string? Detalle, DateTime Fecha);