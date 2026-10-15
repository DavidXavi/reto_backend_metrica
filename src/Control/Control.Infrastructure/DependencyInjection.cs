using BuildingBlocks.Infrastructure.Mensajeria;
using BuildingBlocks.Infrastructure.Web;
using Control.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Control.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddControlInfrastructure(this IServiceCollection s, IConfiguration c)
    {
        s.AgregarPostgres(c);
        s.AgregarMensajeria(c, esquemaOutbox: "control");

        s.AddScoped<ICargaRepositorio, CargaRepositorio>();
        s.AddScoped<ICargaConsultas, CargaConsultas>();

        // Reintentos, circuit breaker y timeouts de Polly en una línea
        s.AddHttpClient<IAlmacenArchivos, SeaweedAlmacen>(h =>
                h.BaseAddress = new Uri(c["Seaweed:FilerUrl"] ?? "http://localhost:8888/"))
            .AddStandardResilienceHandler();

        return s;
    }
}