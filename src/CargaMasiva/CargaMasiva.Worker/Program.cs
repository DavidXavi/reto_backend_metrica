using BuildingBlocks.Core;
using BuildingBlocks.Infrastructure.Web;
using CargaMasiva.Application;
using CargaMasiva.Infrastructure;
using CargaMasiva.Worker;

var builder = WebApplication.CreateBuilder(args);

builder.AgregarComunes("CargaMasiva");
builder.Services.AgregarJwt(builder.Configuration);
builder.Services.AddCargaMasivaApplication();
builder.Services.AddCargaMasivaInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ConsumidorCargaMasiva>();   // el worker vive en el mismo host que la API

var app = builder.Build();
app.UsarComunes();
app.UseAuthentication();
app.UseAuthorization();

// Este servicio es dueño de los productos y de la auditoría: él expone su lectura
var g = app.MapGroup("/api/datos/cargas/{id:int}").RequireAuthorization();

g.MapGet("/registros", async (int id, HttpContext ctx, IDatosConsultas q, CancellationToken ct) =>
    Results.Ok(await q.RegistrosAsync(id, Usuario(ctx), ct)));

g.MapGet("/fallos", async (int id, HttpContext ctx, IDatosConsultas q, CancellationToken ct) =>
    Results.Ok(await q.FallosAsync(id, Usuario(ctx), ct)));

app.Run();

static string Usuario(HttpContext ctx) => ctx.User.Identity?.Name
    ?? throw new NoAutorizadoException("El token no trae email");