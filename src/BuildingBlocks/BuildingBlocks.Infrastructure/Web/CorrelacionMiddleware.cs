using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace BuildingBlocks.Infrastructure.Web;

// Le pone un id a cada petición y lo propaga. El Gateway lo crea, los servicios
// lo reciben en la cabecera y Control lo copia al mensaje de RabbitMQ.
public sealed class CorrelacionMiddleware(RequestDelegate next)
{
    public const string Header = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext ctx)
    {
        var id = ctx.Request.Headers[Header].FirstOrDefault() is { Length: > 0 and <= 100 } h
            ? h
            : Guid.NewGuid().ToString("N");

        ctx.Request.Headers[Header] = id;      // YARP reenvía las cabeceras al servicio interno
        ctx.Response.Headers[Header] = id;
        ctx.TraceIdentifier = id;

        using (LogContext.PushProperty("CorrelationId", id))
            await next(ctx);
    }
}