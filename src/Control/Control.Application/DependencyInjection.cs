using BuildingBlocks.Core;
using Control.Application.Comandos;
using Control.Application.Consultas;
using Microsoft.Extensions.DependencyInjection;

namespace Control.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddControlApplication(this IServiceCollection s)
    {
        s.AddScoped<IHandler<SubirCargaCommand, int>, SubirCargaHandler>();
        s.AddScoped<IHandler<ListarCargasQuery, Pagina<CargaResumen>>, ListarCargasHandler>();
        s.AddScoped<IHandler<ObtenerCargaQuery, CargaDetalle>, ObtenerCargaHandler>();
        s.AddScoped<IHandler<DescargarArchivoQuery, ArchivoDescargado>, DescargarArchivoHandler>();
        return s;
    }
}