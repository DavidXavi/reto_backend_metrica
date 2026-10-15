using Auth.Api.Domain;
using BuildingBlocks.Core;
using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Application;

public sealed record LoginCommand(string Email, string Password);

public sealed class LoginHandler(
    IUsuarioRepositorio usuarios, IPasswordHasher<Usuario> hasher, IEmisorTokens emisor)
    : IHandler<LoginCommand, TokenResponse>
{
    public async Task<TokenResponse> HandleAsync(LoginCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Email) || string.IsNullOrWhiteSpace(cmd.Password))
            throw new ValidacionException("Email y contraseña son obligatorios");

        var usuario = await usuarios.ObtenerPorEmailAsync(cmd.Email.Trim().ToLowerInvariant(), ct);

        // Mismo mensaje si el email no existe o la clave está mal: no se revela cuál falló
        if (usuario is null ||
            hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, cmd.Password)
                == PasswordVerificationResult.Failed)
            throw new NoAutorizadoException("Credenciales inválidas");

        return await emisor.EmitirAsync(usuario, ct);
    }
}

public sealed record RefreshCommand(string RefreshToken);

// Rotación: cada refresh token sirve una sola vez. Si alguien reusa uno ya usado,
// asumimos que lo robaron y cerramos todas las sesiones del usuario.
public sealed class RefreshHandler(
    IUsuarioRepositorio usuarios, IRefreshTokenRepositorio tokens, IEmisorTokens emisor)
    : IHandler<RefreshCommand, TokenResponse>
{
    public async Task<TokenResponse> HandleAsync(RefreshCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.RefreshToken))
            throw new ValidacionException("Falta el refresh token");

        var actual = await tokens.ObtenerAsync(emisor.Hash(cmd.RefreshToken), ct)
            ?? throw new NoAutorizadoException("Refresh token inválido");

        if (actual.Expira < DateTime.UtcNow)
            throw new NoAutorizadoException("Refresh token vencido");

        if (actual.Revocado is not null || !await tokens.RevocarAsync(actual.Id, ct))
        {
            await tokens.RevocarTodosAsync(actual.UsuarioId, ct);
            throw new NoAutorizadoException("Refresh token reutilizado, inicia sesión de nuevo");
        }

        var usuario = await usuarios.ObtenerPorIdAsync(actual.UsuarioId, ct)
            ?? throw new NoAutorizadoException("Usuario no encontrado");
        return await emisor.EmitirAsync(usuario, ct);
    }
}