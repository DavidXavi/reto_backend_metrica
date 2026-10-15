using System.Net;
using BuildingBlocks.Core;
using Control.Application;

namespace Control.Infrastructure;

// SeaweedFS expone un "filer" HTTP: POST para subir, GET para bajar, la ruta es la URL.
public sealed class SeaweedAlmacen(HttpClient http) : IAlmacenArchivos
{
    public async Task SubirAsync(string ruta, byte[] contenido, CancellationToken ct)
    {
        // byte[] y no Stream: un reintento necesita volver a mandar el contenido
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(contenido), "file", Path.GetFileName(ruta) }
        };
        using var resp = await http.PostAsync(ruta, form, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<byte[]> DescargarAsync(string ruta, CancellationToken ct)
    {
        using var resp = await http.GetAsync(ruta, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new NoEncontradoException($"No se encontró el archivo {ruta}");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }
}