namespace Infra.Configure.Env;

public class EmailSettings
{
    public static string ApiKey => "RESEND_API_KEY".GetEnvironmentVariableOrThrow();
    public static string EmailRemetente => "RESEND_FROM_EMAIL".GetEnvironmentVariableOrThrow();
}
