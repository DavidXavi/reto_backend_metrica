using CargaMasiva.Domain;

namespace CargaMasiva.Application;

public interface IAlmacenArchivos
{
    Task<byte[]> DescargarAsync(string ruta, CancellationToken ct);
}

public sealed record FilaCruda(int Fila, object? Periodo, object? Codigo, object? Nombre, object? Precio);

public interface ILectorExcel
{
    IEnumerable<FilaCruda> Leer(byte[] archivo);
}

public interface ICargaRepositorio
{
    // Pendiente -> EnProceso. False si la carga ya fue procesada (mensaje repetido).
    Task<bool> IniciarAsync(int idCarga, CancellationToken ct);

    // Una transacción: staging, reglas, datos, Cargado, Finalizado y mensaje al outbox.
    Task<ResumenCarga> GuardarYCerrarAsync(int idCarga, string usuario,
        IReadOnlyList<FilaProducto> validas, IReadOnlyList<FilaInvalida> invalidas,
        string? correlationId, CancellationToken ct);

    Task CerrarConErrorAsync(int idCarga, string usuario, string detalle, string? correlationId, CancellationToken ct);
}

public interface IDatosConsultas
{
    Task<IReadOnlyList<RegistroDto>> RegistrosAsync(int idCarga, string usuario, CancellationToken ct);
    Task<IReadOnlyList<FalloDto>> FallosAsync(int idCarga, string usuario, CancellationToken ct);
}

public sealed record RegistroDto(int FilaExcel, string Periodo, string CodigoProducto, string NombreProducto, decimal Precio);

public sealed record FalloDto(int? FilaExcel, string? Periodo, string? CodigoProducto, string Motivo, string? Detalle);