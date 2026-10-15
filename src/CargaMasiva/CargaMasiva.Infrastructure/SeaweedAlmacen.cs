using CargaMasiva.Application;

namespace CargaMasiva.Infrastructure;

// Cada microservicio tiene su propio adaptador, aunque se parezca al de Control:
// compartirlo los acoplaría por un archivo de diez líneas.
public sealed class SeaweedAlmacen(HttpClient http) : IAlmacenArchivos
{
    public async Task<byte[]> DescargarAsync(string ruta, CancellationToken ct)
    {
        using var resp = await http.GetAsync(ruta, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }
}