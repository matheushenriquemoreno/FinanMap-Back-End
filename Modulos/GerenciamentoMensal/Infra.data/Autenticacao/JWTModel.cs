namespace Infra.Autenticacao;

public static class JWTModel
{
    public static string SecretKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? throw new Exception("Configurações JWT_KEY faltantes");
    public static string Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? throw new Exception("Configurações JWT_ISSUER faltantes");
    public static string Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? throw new Exception("Configurações JWT_AUDIENCE faltantes");
}
