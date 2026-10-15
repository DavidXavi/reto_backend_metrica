using Auth.Api.Application;
using Auth.Api.Domain;
using Dapper;
using Npgsql;

namespace Auth.Api.Infrastructure;

public sealed class UsuarioRepositorio(NpgsqlDataSource db) : IUsuarioRepositorio
{
    public async Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Usuario>(
            "SELECT id, email, password_hash, rol FROM auth.usuario WHERE email = @email", new { email });
    }

    public async Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Usuario>(
            "SELECT id, email, password_hash, rol FROM auth.usuario WHERE id = @id", new { id });
    }
}

public sealed class RefreshTokenRepositorio(NpgsqlDataSource db) : IRefreshTokenRepositorio
{
    public async Task GuardarAsync(int usuarioId, string tokenHash, DateTime expira, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO auth.refresh_token (usuario_id, token_hash, expira) VALUES (@usuarioId, @tokenHash, @expira)",
            new { usuarioId, tokenHash, expira });
    }

    public async Task<RefreshToken?> ObtenerAsync(string tokenHash, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<RefreshToken>(
            "SELECT id, usuario_id, expira, revocado FROM auth.refresh_token WHERE token_hash = @tokenHash",
            new { tokenHash });
    }

    public async Task<bool> RevocarAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        // el WHERE revocado IS NULL evita que dos refresh simultáneos usen el mismo token
        return await conn.ExecuteAsync(
            "UPDATE auth.refresh_token SET revocado = now() WHERE id = @id AND revocado IS NULL",
            new { id }) == 1;
    }

    public async Task RevocarTodosAsync(int usuarioId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE auth.refresh_token SET revocado = now() WHERE usuario_id = @usuarioId AND revocado IS NULL",
            new { usuarioId });
    }
}