namespace BuildingBlocks.Core;

// Un handler por caso de uso. Comandos (escriben) y consultas (leen) usan la misma
// interfaz, pero viven en carpetas separadas: eso es CQRS sin MediatR.
public interface IHandler<in TRequest, TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken ct);
}