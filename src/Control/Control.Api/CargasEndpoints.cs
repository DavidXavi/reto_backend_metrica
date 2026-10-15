using BuildingBlocks.Core;
using Control.Application;
using Control.Application.Comandos;
using Control.Application.Consultas;

namespace Control.Api;

public static class CargasEndpoints
{
    public static void MapCargasEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/cargas").RequireAuthorization();

        g.MapPost("/", async (IFormFile archivo, HttpContext ctx,
                IHandler<SubirCargaCommand, int> h, CancellationToken ct) =>
            {
                using var ms = new MemoryStream();
                await archivo.CopyToAsync(ms, ct);

                var id = await h.HandleAsync(new SubirCargaCommand(
                    archivo.FileName, ms.ToArray(), Usuario(ctx), IpOrigen(ctx), ctx.TraceIdentifier), ct);

                // 202: recibido, se procesa después. El cliente consulta el estado con el id.
                return Results.Accepted($"/api/cargas/{id}", new { idCarga = id, estado = "Pendiente" });
            })
            .RequireAuthorization("PuedeCargar")
            .DisableAntiforgery();   // API con JWT, sin cookies: no aplica antiforgery

        g.MapGet("/", async (int? pagina, int? tamano, HttpContext ctx,
                IHandler<ListarCargasQuery, Pagina<CargaResumen>> h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(new ListarCargasQuery(Usuario(ctx), pagina ?? 1, tamano ?? 20), ct)));

        g.MapGet("/{id:int}", async (int id, HttpContext ctx,
                IHandler<ObtenerCargaQuery, CargaDetalle> h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(new ObtenerCargaQuery(id, Usuario(ctx)), ct)));

        g.MapGet("/{id:int}/archivo", async (int id, HttpContext ctx,
                IHandler<DescargarArchivoQuery, ArchivoDescargado> h, CancellationToken ct) =>
            {
                var a = await h.HandleAsync(new DescargarArchivoQuery(id, Usuario(ctx)), ct);
                return Results.File(a.Contenido,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", a.Nombre);
            });
    }

    private static string Usuario(HttpContext ctx) => ctx.User.Identity?.Name
        ?? throw new NoAutorizadoException("El token no trae email");

    // Detrás del Gateway la IP real viene en X-Forwarded-For (YARP la agrega)
    private static string? IpOrigen(HttpContext ctx) =>
        ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? ctx.Connection.RemoteIpAddress?.ToString();
}