using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Infra;

public static class IocInjection
{
    /// <summary>
    /// Faz a busca no Assembly informado buscando todas interfaces e implementações,
    /// e injetando automaticamente no serviço de injeção de dependencia.
    /// </summary>
    public static IServiceCollection RegisterApplication(this IServiceCollection services, Assembly application)
    {
        var interfaces = application.GetTypes().Where(item => item.IsInterface);

        foreach (Type interfaceType in interfaces)
        {
            var implementacao = application.GetTypes().FirstOrDefault(x => !x.IsAbstract && interfaceType.IsAssignableFrom(x));

            if (implementacao is not null)
                services.AddScoped(interfaceType, implementacao);
        }

        return services;
    }

    /// <summary>
    /// Registras as dependencias dos repositórios automaticamente, com base no assembly de interfaces e implementações.
    /// </summary>
    /// <param name="assemblyInterfaces"></param>
    /// <param name="assemblyImplementations"></param>
    /// <param name="services"></param>
    public static IServiceCollection RegisterRepository(this IServiceCollection services, Assembly assemblyInterfaces, Assembly assemblyImplementations)
    {
        var interfaces = GetInterfaces(assemblyInterfaces);
        var repositories = GetRepositories(assemblyImplementations);

        foreach (var interfaceType in interfaces)
        {
            var implementations = repositories.Where(interfaceType.IsAssignableFrom);

            foreach (var implementation in implementations)
            {
                services.AddScoped(interfaceType, implementation);
            }
        }


        return services;
    }

    private static IEnumerable<Type> GetInterfaces(Assembly assemblyInterfaces)
    {
        var applicationInterfaces = assemblyInterfaces
            .GetTypes()
            .Where(t => t.IsInterface && t.Name.StartsWith("I") && t.Name.Contains("Repository"));

        return applicationInterfaces;
    }

    private static IEnumerable<Type> GetRepositories(Assembly implementacoesAssembly)
    {
        var repositoriesAplicacao = implementacoesAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.Contains("Repository") && !t.Name.Contains("Cached"));

        return repositoriesAplicacao;
    }
}
