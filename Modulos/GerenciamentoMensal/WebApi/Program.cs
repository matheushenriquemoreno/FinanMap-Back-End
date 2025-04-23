using System.Text;
using System.Text.Json.Serialization;
using Domain.Login.Interfaces;
using Infra;
using Infra.Autenticacao;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using WebApi.Configs.ExecptionHandler;
using WebApi.Controllers;
using WebApi.Controlles;
using WebApi.Interceptor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddAuthentication(
    options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    }
    )
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(JWTModel.SecretKey)),

            ValidateIssuer = true, 
            ValidIssuer = JWTModel.Issuer,  

            ValidateAudience = true, 
            ValidAudience = JWTModel.Audience, 

            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero 
   
        };
        // logins de validação de token
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                Console.WriteLine("Token validado com sucesso, valido ate: " + context.SecurityToken.ValidTo);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();
builder.Services.AddOpenApi();

builder.Services.RegistrarDependencias();

builder.Services.AddScoped<IUsuarioLogado, UsuarioLogado>();

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddExceptionHandler<AutenticacaoExecptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services
    .AddHealthChecks()
    .AddMongoDb();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder
        .WithOrigins(
            "http://localhost:9000", 
                    "http://192.168.100.3:9070")
        .AllowCredentials()
        .AllowAnyHeader()
        .AllowAnyMethod()
        .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

var app = builder.Build();


app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Servers = Array.Empty<ScalarServer>();
    });
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.MapLoginEndpoints()
    .WithTags("Login")
    .WithOpenApi();

app.MapCategoriaEndpoints()
      .WithTags("Categorias")
      .WithOpenApi()
      .RequireAuthorization();

app.MapRendimentoEndpoints()
      .WithTags("Rendimentos")
      .WithOpenApi()
      .RequireAuthorization();

app.MapDespesaEndpoints()
      .WithTags("Despesas")
      .WithOpenApi()
      .RequireAuthorization();

app.MapInvestimentoEndpoints()
    .WithTags("Investimentos")
    .WithOpenApi()
    .RequireAuthorization();

app.MapAcumuladoMensalEndpoints()
    .WithTags("AcumuladoMensalReport")
    .WithOpenApi()
    .RequireAuthorization();


app.MapUsuarioEndpoints()
    .WithTags("Usuario")
    .WithOpenApi()
    .RequireAuthorization();


app.MapReplicarTransacaoEndpoints()
    .WithTags("ReplicarTranscao")
    .WithOpenApi()
    .RequireAuthorization();

app.MapHealthChecks("/health");

app.Run();
