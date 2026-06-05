using Microsoft.AspNetCore.Diagnostics;
using WebApi.Configs.Models;

namespace WebApi.Configs.ExecptionHandler
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var apiError = ApiResultError.Create(exception.Message);
            var statusCode = StatusCodes.Status500InternalServerError;
            _logger.LogError(
                exception,
                "Erro nao tratado em {Method} {Path}. StatusCode: {StatusCode}. Message: {Message}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                statusCode,
                exception.Message);

            if (exception.InnerException is not null)
                apiError.AddErro(exception.InnerException.Message);

            httpContext.Response.StatusCode = statusCode;

            await httpContext.Response
                .WriteAsJsonAsync(apiError, cancellationToken);

            return true;
        }
    }
}
