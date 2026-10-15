namespace CargaMasiva.Domain;

public static class Resultados
{
    public const string Exitoso = "Exitoso";
    public const string ConObservaciones = "ConObservaciones";
    public const string Rechazado = "Rechazado";
    public const string Error = "Error";

    // La función de la base solo ve las filas válidas. Si además hubo filas
    // inválidas, el resultado final no puede ser Exitoso.
    public static string Decidir(string resultadoBd, int filasValidas, int filasInvalidas) =>
        filasValidas == 0 ? Rechazado
        : resultadoBd == Exitoso && filasInvalidas > 0 ? ConObservaciones
        : resultadoBd;
}

public sealed record ResumenCarga(string Resultado, int Total, int Insertadas, int Existentes, int Invalidas);