using System.Reflection;
using Application.Email.Interfaces;
using Application.IA;
using Application.Login.Interfaces;
using Domain.Repository;
using Infra.Autenticacao;
using Infra.Cache.Repository;
using Infra.Data.Mongo.Config;
using Infra.Data.Mongo.Repositorys;
using Infra.Email;
using Infra.IA;
using Infra.MediaTrConfigure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Infra;

public static class Setup
{
    public static IServiceCollection RegistrarDependencias(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddScoped<IServiceJWT, ServiceJWT>();
        services.AddScoped<IProvedorEmail, SmtpGmailProvedor>();
        services.AddSingleton<IAGenerativaChat, ChatGPT>();

        services.RegisterApplication(Assembly.Load("Application"));
        services.RegisterRepository(assemblyInterfaces: Assembly.Load("Domain"), assemblyImplementations: Assembly.Load("Infra"));

        #region Configurando estrategia de cache para categoria

        services.TryAddScoped<ICategoriaRepository, CategoriaRepository>();
        services.Decorate<ICategoriaRepository, CachedCategoriaRepository>();

        #endregion




        services.ConfiguarMongoDB();
        services.ConfigureMediaTR();

        return services;
    }
}
