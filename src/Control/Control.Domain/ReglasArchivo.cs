using BuildingBlocks.Core;

namespace Control.Domain;

public static class EstadoCarga
{
    public const string Pendiente = "Pendiente";
    public const string EnProceso = "EnProceso";
    public const string Cargado = "Cargado";
    public const string Finalizado = "Finalizado";
    public const string Notificado = "Notificado";
}

// Reglas del archivo subido. Lógica pura: no sabe de HTTP ni de base de datos,
// por eso se prueba con un test unitario simple.
public static class ReglasArchivo
{
    public static void Validar(string nombreArchivo, byte[] contenido, long tamanoMaximoMb)
    {
        if (contenido.Length == 0)
            throw new ValidacionException("El archivo está vacío");

        if (contenido.Length > tamanoMaximoMb * 1024 * 1024)
            throw new ArchivoMuyGrandeException(tamanoMaximoMb);

        if (!nombreArchivo.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ValidacionException("Solo se aceptan archivos .xlsx");

        // Un .xlsx es un ZIP: empieza con los bytes "PK". Así no pasa un archivo renombrado.
        if (contenido.Length < 2 || contenido[0] != (byte)'P' || contenido[1] != (byte)'K')
            throw new ValidacionException("El archivo no es un Excel válido");
    }
}

public sealed record NuevaCarga(
    string NombreArchivo, string RutaArchivo, long TamanoBytes, string Usuario, string? Ip);