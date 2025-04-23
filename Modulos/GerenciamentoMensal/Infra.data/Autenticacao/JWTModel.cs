namespace Infra.Autenticacao;

public static class JWTModel
{
    public static string SecretKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? throw new Exception("Configurações JWT faltantes");
    public static string Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? throw new Exception("Configurações JWT faltantes");
    public static string Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? throw new Exception("Configurações JWT faltantes");
}
