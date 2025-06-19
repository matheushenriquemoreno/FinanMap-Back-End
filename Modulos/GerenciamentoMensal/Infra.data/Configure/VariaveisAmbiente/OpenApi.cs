namespace Infra.Configure.VariaveisAmbiente;

public class OpenApi
{
    public static string Key => "GPT_KEY".GetEnvironmentVariableOrThrow();
}
