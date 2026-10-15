using BuildingBlocks.Core;

namespace Control.Application.Consultas;

public sealed record ListarCargasQuery(string Usuario, int Pagina, int Tamano);

public sealed class ListarCargasHandler(ICargaConsultas consultas)
    : IHandler<ListarCargasQuery, Pagina<CargaResumen>>
{
    public Task<Pagina<CargaResumen>> HandleAsync(ListarCargasQuery q, CancellationToken ct) =>
        consultas.ListarAsync(q.Usuario, Math.Max(q.Pagina, 1), Math.Clamp(q.Tamano, 1, 100), ct);
}

public sealed record ObtenerCargaQuery(int Id, string Usuario);

public sealed class ObtenerCargaHandler(ICargaConsultas consultas)
    : IHandler<ObtenerCargaQuery, CargaDetalle>
{
    public async Task<CargaDetalle> HandleAsync(ObtenerCargaQuery q, CancellationToken ct) =>
        await consultas.ObtenerAsync(q.Id, q.Usuario, ct)
        ?? throw new NoEncontradoException($"No existe la carga {q.Id}");
}

public sealed record DescargarArchivoQuery(int Id, string Usuario);

public sealed record ArchivoDescargado(string Nombre, byte[] Contenido);

public sealed class DescargarArchivoHandler(ICargaConsultas consultas, IAlmacenArchivos almacen)
    : IHandler<DescargarArchivoQuery, ArchivoDescargado>
{
    public async Task<ArchivoDescargado> HandleAsync(DescargarArchivoQuery q, CancellationToken ct)
    {
        var carga = await consultas.ObtenerAsync(q.Id, q.Usuario, ct)
            ?? throw new NoEncontradoException($"No existe la carga {q.Id}");
        return new ArchivoDescargado(carga.NombreArchivo, await almacen.DescargarAsync(carga.RutaArchivo, ct));
    }
}