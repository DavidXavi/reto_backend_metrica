using BuildingBlocks.Core;
using BuildingBlocks.Infrastructure.Web;
using Control.Api;
using Control.Application;
using Control.Domain;
using Control.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AgregarComunes("Control");
builder.Services.AgregarJwt(builder.Configuration);   // defensa en profundidad: valida aunque el Gateway ya validó
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("PuedeCargar", p => p.RequireRole("Cargador"));

builder.Services.Configure<CargaOptions>(builder.Configuration.GetSection("Carga"));
builder.Services.AddControlApplication();
builder.Services.AddControlInfrastructure(builder.Configuration);

var app = builder.Build();
app.UsarComunes();
app.UseAuthentication();
app.UseAuthorization();
app.MapCargasEndpoints();
app.Run();