namespace Infra;

public static class Helper
{
    public static string GetEnvironmentVariableOrThrow(this string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);

        ArgumentNullException.ThrowIfNull(value);

        return value;
    }
}
