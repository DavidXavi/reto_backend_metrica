using BuildingBlocks.Infrastructure.Mensajeria;
using BuildingBlocks.Infrastructure.Web;
using CargaMasiva.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CargaMasiva.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCargaMasivaInfrastructure(this IServiceCollection s, IConfiguration c)
    {
        s.AgregarPostgres(c);
        s.AgregarMensajeria(c, esquemaOutbox: "carga");

        s.AddScoped<ICargaRepositorio, CargaRepositorio>();
        s.AddScoped<IDatosConsultas, DatosConsultas>();
        s.AddSingleton<ILectorExcel, LectorExcel>();

        s.AddHttpClient<IAlmacenArchivos, SeaweedAlmacen>(h =>
                h.BaseAddress = new Uri(c["Seaweed:FilerUrl"] ?? "http://localhost:8888/"))
            .AddStandardResilienceHandler();

        return s;
    }
}