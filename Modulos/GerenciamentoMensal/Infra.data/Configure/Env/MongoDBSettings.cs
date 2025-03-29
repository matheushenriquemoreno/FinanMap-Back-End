namespace Infra.Configure.Env;

internal class MongoDBSettings
{
    public static string ConnectionString => Environment.GetEnvironmentVariable("MONGO_URL");
    public static string DataBaseName => "FinanMap";
}
