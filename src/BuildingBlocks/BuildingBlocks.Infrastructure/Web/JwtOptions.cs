namespace BuildingBlocks.Infrastructure.Web;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string Key { get; init; } = "";           // mínimo 32 caracteres, llega por variable de entorno
    public int MinutosAcceso { get; init; } = 15;
    public int DiasRefresh { get; init; } = 7;
}