using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Serilog;
using Serilog.Formatting.Compact;

namespace BuildingBlocks.Infrastructure.Web;

public static class ExtensionesWeb
{
    // Logging estructurado, manejo global de excepciones y ProblemDetails.
    public static WebApplicationBuilder AgregarComunes(this WebApplicationBuilder b, string servicio)
    {
        b.Services.AddSerilog(lc => lc
            .ReadFrom.Configuration(b.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Servicio", servicio)
            .WriteTo.Console(new CompactJsonFormatter()));

        b.Services.AddExceptionHandler<ManejadorGlobal>();
        b.Services.AddProblemDetails();
        return b;
    }

    public static WebApplication UsarComunes(this WebApplication app)
    {
        app.UseMiddleware<CorrelacionMiddleware>();
        app.UseExceptionHandler();
        app.UseSerilogRequestLogging();
        app.MapHealthChecks("/health");
        return app;
    }

    // Un NpgsqlDataSource por servicio (maneja el pool de conexiones) y health check.
    public static IServiceCollection AgregarPostgres(this IServiceCollection s, IConfiguration c)
    {
        var cadena = c.GetConnectionString("Reto")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Reto");

        DefaultTypeMap.MatchNamesWithUnderscores = true;   // nombre_archivo -> NombreArchivo
        s.AddSingleton(NpgsqlDataSource.Create(cadena));
        s.AddHealthChecks().AddNpgSql(cadena);
        return s;
    }

    public static IServiceCollection AgregarJwt(this IServiceCollection s, IConfiguration c)
    {
        var jwt = c.GetSection("Jwt").Get<JwtOptions>()
            ?? throw new InvalidOperationException("Falta la sección Jwt");
        s.Configure<JwtOptions>(c.GetSection("Jwt"));

        s.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;            // conserva "email" y "role" tal cual
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    NameClaimType = "email",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        s.AddAuthorization();
        return s;
    }
}