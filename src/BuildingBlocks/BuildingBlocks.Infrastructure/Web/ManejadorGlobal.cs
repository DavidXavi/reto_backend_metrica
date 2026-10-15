using BuildingBlocks.Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Web;

// Un solo lugar convierte excepciones en respuestas ProblemDetails (RFC 9457).
// Los endpoints y handlers no llevan try/catch.
public sealed class ManejadorGlobal(ILogger<ManejadorGlobal> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, titulo) = ex switch
        {
            ArchivoMuyGrandeException => (StatusCodes.Status413PayloadTooLarge, "Archivo demasiado grande"),
            ValidacionException       => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            NoAutorizadoException     => (StatusCodes.Status401Unauthorized, "No autorizado"),
            NoEncontradoException     => (StatusCodes.Status404NotFound, "No encontrado"),
            BadHttpRequestException b => (b.StatusCode, "Solicitud inválida"),
            _                         => (StatusCodes.Status500InternalServerError, "Error interno")
        };

        if (status >= 500) log.LogError(ex, "Error no controlado");
        else log.LogWarning("{Titulo}: {Mensaje}", titulo, ex.Message);

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Detail = status >= 500 ? null : ex.Message,   // no se filtran detalles internos
            Instance = ctx.Request.Path
        }, ct);
        return true;
    }
}