using System.Security.Cryptography;
using System.Text;
using Auth.Api.Application;
using Auth.Api.Domain;
using BuildingBlocks.Infrastructure.Web;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Api.Infrastructure;

public sealed class EmisorTokens(IOptions<JwtOptions> opciones, IRefreshTokenRepositorio refreshTokens)
    : IEmisorTokens
{
    public async Task<TokenResponse> EmitirAsync(Usuario usuario, CancellationToken ct)
    {
        var jwt = opciones.Value;
        var llave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));

        var accessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Expires = DateTime.UtcNow.AddMinutes(jwt.MinutosAcceso),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = usuario.Id.ToString(),
                ["email"] = usuario.Email,
                ["role"] = usuario.Rol
            },
            SigningCredentials = new SigningCredentials(llave, SecurityAlgorithms.HmacSha256)
        });

        // El refresh token es aleatorio. En la base solo se guarda su hash.
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        await refreshTokens.GuardarAsync(usuario.Id, Hash(refreshToken),
            DateTime.UtcNow.AddDays(jwt.DiasRefresh), ct);

        return new TokenResponse(accessToken, refreshToken, jwt.MinutosAcceso * 60, usuario.Email, usuario.Rol);
    }

    public string Hash(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}