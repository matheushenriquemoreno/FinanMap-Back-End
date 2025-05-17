namespace Infra.Configure.Env;

internal class MongoDBSettings
{
    public static string ConnectionString => "MONGO_URL".GetEnvironmentVariableOrThrow();
    public static string DataBaseName => "FinanMap";
}
