using System.Security.Claims;
using System.Threading.RateLimiting;
using BuildingBlocks.Infrastructure.Web;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.AgregarComunes("Gateway");
builder.Services.AgregarJwt(config);
builder.Services.AddHealthChecks();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("autenticado", p => p.RequireAuthenticatedUser());

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(config.GetSection("Cors:Origenes").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders(CorrelacionMiddleware.Header)));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Por usuario si está autenticado, por IP si no
    o.AddPolicy("por-usuario", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirstValue("email") ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = config.GetValue("RateLimit:PorMinuto", 60),
            Window = TimeSpan.FromMinutes(1)
        }));

    // El login es más estricto: frena ataques de fuerza bruta
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = config.GetValue("RateLimit:LoginPorMinuto", 10),
            Window = TimeSpan.FromMinutes(1)
        }));
});

builder.Services.AddReverseProxy().LoadFromConfig(config.GetSection("ReverseProxy"));

var app = builder.Build();

app.UsarComunes();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapReverseProxy();

app.Run();