using Auth.Api.Application;
using Auth.Api.Domain;
using Auth.Api.Infrastructure;
using BuildingBlocks.Core;
using BuildingBlocks.Infrastructure.Web;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.AgregarComunes("Auth");
builder.Services.AgregarPostgres(builder.Configuration);
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

// Composition root: aquí se conectan interfaces con implementaciones
builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
builder.Services.AddScoped<IRefreshTokenRepositorio, RefreshTokenRepositorio>();
builder.Services.AddScoped<IEmisorTokens, EmisorTokens>();
builder.Services.AddScoped<IHandler<LoginCommand, TokenResponse>, LoginHandler>();
builder.Services.AddScoped<IHandler<RefreshCommand, TokenResponse>, RefreshHandler>();

var app = builder.Build();
app.UsarComunes();

app.MapPost("/auth/login", async (LoginCommand cmd, IHandler<LoginCommand, TokenResponse> h, CancellationToken ct)
    => Results.Ok(await h.HandleAsync(cmd, ct)));

app.MapPost("/auth/refresh", async (RefreshCommand cmd, IHandler<RefreshCommand, TokenResponse> h, CancellationToken ct)
    => Results.Ok(await h.HandleAsync(cmd, ct)));

await SembradorUsuarios.SembrarAsync(app.Services, app.Configuration);
app.Run();