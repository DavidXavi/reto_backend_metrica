using BuildingBlocks.Core;
using CargaMasiva.Application;
using MiniExcelLibs;

namespace CargaMasiva.Infrastructure;

// MiniExcel lee en streaming: no carga todo el archivo en memoria.
public sealed class LectorExcel : ILectorExcel
{
    private static readonly string[] Columnas = ["Periodo", "CodigoProducto", "NombreProducto", "Precio"];

    public IEnumerable<FilaCruda> Leer(byte[] archivo)
    {
        using var stream = new MemoryStream(archivo);
        var fila = 1;                                   // la fila 1 es el encabezado
        var encabezadoRevisado = false;

        foreach (IDictionary<string, object?> r in stream.Query(useHeaderRow: true))
        {
            if (!encabezadoRevisado)
            {
                var faltan = Columnas.Where(c => !r.ContainsKey(c)).ToList();
                if (faltan.Count > 0)
                    throw new ValidacionException($"Faltan columnas: {string.Join(", ", faltan)}");
                encabezadoRevisado = true;
            }

            fila++;
            yield return new FilaCruda(fila, r["Periodo"], r["CodigoProducto"], r["NombreProducto"], r["Precio"]);
        }
    }
}