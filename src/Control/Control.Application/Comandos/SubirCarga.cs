using BuildingBlocks.Core;
using Control.Domain;
using Microsoft.Extensions.Options;

namespace Control.Application.Comandos;

public sealed record SubirCargaCommand(
    string NombreArchivo, byte[] Contenido, string Usuario, string? Ip, string? CorrelationId);

public sealed class SubirCargaHandler(
    IAlmacenArchivos almacen, ICargaRepositorio repo, IOptions<CargaOptions> opciones)
    : IHandler<SubirCargaCommand, int>
{
    public async Task<int> HandleAsync(SubirCargaCommand cmd, CancellationToken ct)
    {
        ReglasArchivo.Validar(cmd.NombreArchivo, cmd.Contenido, opciones.Value.TamanoMaximoMb);

        // Primero el archivo, después la base. Si la base falla queda un archivo huérfano
        // (inofensivo). Al revés quedaría una carga apuntando a un archivo que no existe.
        var ruta = $"cargas/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}.xlsx";
        await almacen.SubirAsync(ruta, cmd.Contenido, ct);

        var carga = new NuevaCarga(cmd.NombreArchivo, ruta, cmd.Contenido.Length, cmd.Usuario, cmd.Ip);
        return await repo.RegistrarPendienteAsync(carga, cmd.CorrelationId, ct);
    }
}