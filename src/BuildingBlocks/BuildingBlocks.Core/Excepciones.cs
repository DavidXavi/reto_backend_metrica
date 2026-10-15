namespace BuildingBlocks.Core;

// Excepciones de negocio. No saben de HTTP: el manejador global decide el código.
public class ValidacionException(string mensaje) : Exception(mensaje);

public sealed class ArchivoMuyGrandeException(long maximoMb)
    : ValidacionException($"El archivo supera el máximo de {maximoMb} MB");

public sealed class NoEncontradoException(string mensaje) : Exception(mensaje);

public sealed class NoAutorizadoException(string mensaje) : Exception(mensaje);