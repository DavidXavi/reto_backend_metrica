using Auth.Api.Domain;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace Auth.Api.Infrastructure;

// Crea los usuarios de prueba al arrancar. La clave llega por variable de entorno
public static class SembradorUsuarios
{
    public static async Task SembrarAsync(IServiceProvider sp, IConfiguration config)
    {
        var clave = config["Semilla:Clave"];
        if (string.IsNullOrEmpty(clave)) return;

        var db = sp.GetRequiredService<NpgsqlDataSource>();
        var hasher = sp.GetRequiredService<IPasswordHasher<Usuario>>();
        await using var conn = await db.OpenConnectionAsync();

        foreach (var (email, rol) in new[] { ("cargador@reto.pe", Roles.Cargador), ("consulta@reto.pe", Roles.Consulta) })
        {
            var usuario = new Usuario { Email = email, Rol = rol };
            usuario.PasswordHash = hasher.HashPassword(usuario, clave);
            await conn.ExecuteAsync(
                """
                INSERT INTO auth.usuario (email, password_hash, rol)
                VALUES (@Email, @PasswordHash, @Rol)
                ON CONFLICT (email) DO NOTHING
                """, usuario);
        }
    }
}