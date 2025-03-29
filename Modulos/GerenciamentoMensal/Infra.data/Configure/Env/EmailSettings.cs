namespace Infra.Configure.Env;

public class EmailSettings
{
    public static string Email => "Email_SMTP".GetEnvironmentVariableOrThrow();
    public static string Password => "Password_SMTP".GetEnvironmentVariableOrThrow();
}
