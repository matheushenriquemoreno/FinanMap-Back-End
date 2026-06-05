using Microsoft.AspNetCore.Diagnostics;
using SharedDomain.Exceptions;
using WebApi.Configs.Models;

namespace WebApi.Configs.ExecptionHandler
{
    public class AutenticacaoExecptionHandler : IExceptionHandler
    {
        private readonly ILogger<AutenticacaoExecptionHandler> _logger;

        public AutenticacaoExecptionHandler(ILogger<AutenticacaoExecptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is not AutenticacaoNecessariaException)
            {
                return false;
            }

            var apiError = ApiResultError.Create(exception.Message);

            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            _logger.LogWarning(
                exception,
                "Erro de autenticacao em {Method} {Path}. StatusCode: {StatusCode}. Message: {Message}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                StatusCodes.Status401Unauthorized,
                exception.Message);

            await httpContext.Response
                .WriteAsJsonAsync(apiError, cancellationToken);

            return true;
        }
    }
}
