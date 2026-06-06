using System.Reflection;
using Application.Email.Interfaces;
using Application.Login.Interfaces;
using Domain.Repository;
using Infra.Autenticacao;
using Infra.Cache.Repository;
using Infra.Data.Mongo.Config;
using Infra.Data.Mongo.Repositorys;
using Infra.Email;
using Infra.MediaTrConfigure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Resend;

namespace Infra;

public static class Setup
{
    public static IServiceCollection RegistrarDependencias(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddScoped<IServiceJWT, ServiceJWT>();

        // Configuração do Resend (HTTP API para envio de e-mails)
        services.AddOptions();
        services.AddHttpClient<ResendClient>();
        services.Configure<ResendClientOptions>(o =>
        {
            o.ApiToken = Infra.Configure.Env.EmailSettings.ApiKey;
        });
        services.AddTransient<IResend, ResendClient>();
        services.AddScoped<IProvedorEmail, ResendEmailProvedor>();

        services.RegisterApplication(Assembly.Load("Application"));
        services.RegisterRepository(assemblyInterfaces: Assembly.Load("Domain"), assemblyImplementations: Assembly.Load("Infra"));

        #region Configurando estrategia de cache para categoria

        services.TryAddScoped<ICategoriaRepository, CategoriaRepository>();
        services.Decorate<ICategoriaRepository, CachedCategoriaRepository>();

        #endregion

        #region Configurando estrategia de cache para usuario

        services.TryAddScoped<IUsuarioRepository, UsuarioRepository>();
        services.Decorate<IUsuarioRepository, CachedUsuarioRepository>();

        #endregion

        services.ConfiguarMongoDB();
        services.ConfigureMediaTR();

        return services;
    }
}
