using System.Reflection;
using Infra.Configure.Env;
using Infra.Data.Mongo.Config.Interface;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Config;

public static class MongoConfig
{
    private static void MappingAllClassMongo(this IServiceCollection services, IMongoClient mongoClient)
    {
        Task.Run(() =>
        {
            try
            {
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
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ocorreu um erro ao registrar as classes, indexs, e configurações do Mongo DB.");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.InnerException?.Message);
            }
        });



    }

    public static void ConfiguarMongoDB(this IServiceCollection services)
    {
        IMongoClient mongoClient = new MongoClient(MongoDBSettings.ConnectionString);

        services.AddSingleton(mongoClient);

        services.MappingAllClassMongo(mongoClient);
    }
}
