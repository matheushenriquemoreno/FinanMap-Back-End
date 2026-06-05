using System.Reflection;
using Infra.Configure.Env;
using Infra.Data.Mongo.Config.Interface;
using Infra.Data.Mongo.Mappings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Config;

public static class MongoConfig
{
    private static void MappingAllClassMongo(IMongoClient mongoClient, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(MongoConfig).FullName!);

        try
        {
            TransacaoMapping.ConfigureLogger(loggerFactory.CreateLogger("Infra.Data.Mongo.Mappings.TransacaoMapping"));

            logger.LogInformation("Mapping do MongoDB inicializado.");

            var assembly = Assembly.GetExecutingAssembly();

            #region Mapeamento de entidades Bases

            var classesMapeadoras = assembly.GetTypes()
                .Where(t => typeof(IMongoMappingClassBase).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            foreach (var mapping in classesMapeadoras)
            {
                var instancia = Activator.CreateInstance(mapping) as IMongoMappingClassBase;
                instancia?.RegisterMap(mongoClient);
            }

            #endregion

            classesMapeadoras = assembly.GetTypes()
                .Where(t => typeof(IMongoMapping).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();


            foreach (var mapping in classesMapeadoras)
            {
                var instancia = Activator.CreateInstance(mapping) as IMongoMapping;
                instancia?.RegisterMap(mongoClient);
            }

            logger.LogInformation("Mapping do MongoDB finalizado com sucesso.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocorreu um erro ao registrar as classes, indexes e configuracoes do MongoDB.");
        }

    }

    public static void ConfiguarMongoDB(this IServiceCollection services)
    {
        services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var mongoClient = new MongoClient(MongoDBSettings.ConnectionString);
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            MappingAllClassMongo(mongoClient, loggerFactory);

            return mongoClient;
        });
    }
}
