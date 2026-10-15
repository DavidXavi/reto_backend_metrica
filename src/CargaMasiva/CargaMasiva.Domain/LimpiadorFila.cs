using System.Globalization;
using System.Text.RegularExpressions;

namespace CargaMasiva.Domain;

public sealed record FilaProducto(int Fila, string Periodo, string Codigo, string Nombre, decimal Precio);

public sealed record FilaInvalida(int Fila, string? Periodo, string? Codigo, string Motivo);

public enum TipoFila { Valida, Invalida, Omitida }

public sealed record ResultadoFila(TipoFila Tipo, FilaProducto? Valida = null, FilaInvalida? Invalida = null);

// Limpieza y validación de una fila del Excel. Lógica pura: se prueba sin base de datos.
public static partial class LimpiadorFila
{
    public const string NombrePorDefecto = "SIN NOMBRE";
    public const decimal PrecioPorDefecto = 0m;

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex FormatoPeriodo();

    public static ResultadoFila Limpiar(int fila, object? periodo, object? codigo, object? nombre, object? precio)
    {
        if (EsVacio(periodo) && EsVacio(codigo) && EsVacio(nombre) && EsVacio(precio))
            return new ResultadoFila(TipoFila.Omitida);                    // fila vacía: no se registra

        var c = codigo?.ToString()?.Trim().ToUpperInvariant();
        var p = NormalizarPeriodo(periodo);

        if (p is null)
            return Invalida(fila, periodo?.ToString(), c, "Periodo vacío o distinto de AAAA-MM");
        if (string.IsNullOrEmpty(c))
            return Invalida(fila, p, null, "Código vacío");
        if (c.Length > 20)
            return Invalida(fila, p, c, "Código de más de 20 caracteres");

        var n = EsVacio(nombre) ? NombrePorDefecto : nombre!.ToString()!.Trim();
        if (n.Length > 200) n = n[..200];

        decimal pr;
        if (EsVacio(precio)) pr = PrecioPorDefecto;                       // celda vacía: valor por defecto
        else if (!TryDecimal(precio!, out pr)) return Invalida(fila, p, c, "Precio no numérico");
        if (pr < 0) return Invalida(fila, p, c, "Precio negativo");

        return new ResultadoFila(TipoFila.Valida, new FilaProducto(fila, p, c, n, Math.Round(pr, 2)));
    }

    // Si alguien escribe 2025-03 a mano, Excel lo guarda como fecha. Aceptamos los dos.
    public static string? NormalizarPeriodo(object? valor) => valor switch
    {
        DateTime d => d.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        string s when FormatoPeriodo().IsMatch(s.Trim()) => s.Trim(),
        _ => null
    };

    private static bool TryDecimal(object valor, out decimal resultado)
    {
        switch (valor)
        {
            case decimal d: resultado = d; return true;
            case double d: resultado = (decimal)d; return true;
            case int i: resultado = i; return true;
            case long l: resultado = l; return true;
        }
        // Texto: "12.50" o "12,50". Sin punto, la coma se toma como decimal
        // (con InvariantCulture "12,50" se leería como 1250).
        var texto = valor.ToString()!.Trim();
        if (!texto.Contains('.')) texto = texto.Replace(',', '.');
        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out resultado);
    }

    private static bool EsVacio(object? v) => v is null || (v is string s && string.IsNullOrWhiteSpace(s));

    private static ResultadoFila Invalida(int fila, string? periodo, string? codigo, string motivo) =>
        new(TipoFila.Invalida, Invalida: new FilaInvalida(fila, periodo?.Length > 7 ? null : periodo, codigo, motivo));
}