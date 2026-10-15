using BuildingBlocks.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CargaMasiva.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCargaMasivaApplication(this IServiceCollection s)
    {
        s.AddScoped<IHandler<ProcesarCargaCommand, bool>, ProcesarCargaHandler>();
        return s;
    }
}