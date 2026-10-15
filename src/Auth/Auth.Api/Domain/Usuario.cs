namespace Auth.Api.Domain;

public sealed class Usuario
{
    public int Id { get; init; }
    public string Email { get; init; } = "";
    public string PasswordHash { get; set; } = "";
    public string Rol { get; init; } = "";
}

public static class Roles
{
    public const string Cargador = "Cargador";   // puede subir archivos
    public const string Consulta = "Consulta";   // solo ve el historial
}

public sealed class RefreshToken
{
    public long Id { get; init; }
    public int UsuarioId { get; init; }
    public DateTime Expira { get; init; }
    public DateTime? Revocado { get; init; }
}