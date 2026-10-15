using BuildingBlocks.Core;
using BuildingBlocks.Infrastructure.Mensajeria;
using BuildingBlocks.Infrastructure.Web;
using Contracts;
using Notificaciones.Worker;
using Notificaciones.Worker.Application;
using Notificaciones.Worker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AgregarComunes("Notificaciones");
builder.Services.AgregarPostgres(builder.Configuration);
builder.Services.AgregarMensajeria(builder.Configuration);    // solo consume, no necesita outbox
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

builder.Services.AddScoped<IEnviadorCorreo, EnviadorCorreo>();
builder.Services.AddScoped<ICargaEstados, CargaEstados>();
builder.Services.AddScoped<IHandler<CargaFinalizada, bool>, NotificarCargaHandler>();
builder.Services.AddHostedService<ConsumidorNotificaciones>();

var app = builder.Build();
app.UsarComunes();   // solo expone /health
app.Run();