using DbUp;

// Corre una vez al levantar Compose y termina. DbUp guarda en la tabla
// schemaversions qué scripts ya aplicó, así que cada arranque aplica solo lo nuevo.
var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Reto")
    ?? throw new InvalidOperationException("Falta la variable ConnectionStrings__Reto");
var carpeta = Environment.GetEnvironmentVariable("CARPETA_SCRIPTS") ?? "db/scripts";

EnsureDatabase.For.PostgresqlDatabase(cadena);

var resultado = DeployChanges.To
    .PostgresqlDatabase(cadena)
    .WithScriptsFromFileSystem(carpeta)
    .WithTransactionPerScript()
    .LogToConsole()
    .Build()
    .PerformUpgrade();

if (!resultado.Successful)
{
    Console.Error.WriteLine(resultado.Error);
    return 1;
}

Console.WriteLine("Base de datos al día");
return 0;