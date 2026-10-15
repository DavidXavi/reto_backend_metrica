using Auth.Api.Domain;

namespace Auth.Api.Application;

public interface IUsuarioRepositorio
{
    Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct);
    Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken ct);
}

public interface IRefreshTokenRepositorio
{
    Task GuardarAsync(int usuarioId, string tokenHash, DateTime expira, CancellationToken ct);
    Task<RefreshToken?> ObtenerAsync(string tokenHash, CancellationToken ct);
    Task<bool> RevocarAsync(long id, CancellationToken ct);      // false si ya estaba revocado
    Task RevocarTodosAsync(int usuarioId, CancellationToken ct);
}

public interface IEmisorTokens
{
    Task<TokenResponse> EmitirAsync(Usuario usuario, CancellationToken ct);
    string Hash(string refreshToken);
}

public sealed record TokenResponse(
    string AccessToken, string RefreshToken, int ExpiraEnSegundos, string Email, string Rol);