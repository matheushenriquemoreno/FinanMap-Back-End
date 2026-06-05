using Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using WebApi.Configs.Models;

namespace WebApi.Configs.ExecptionHandler
{
    public class DomainExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<DomainExceptionHandler> _logger;

        public DomainExceptionHandler(ILogger<DomainExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is not DomainValidatorException)
            {
                return false;
            }

            var erros = (exception as DomainValidatorException).Errors;

            var apiError = ApiResultError.Create(erros);

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            await httpContext.Response
                .WriteAsJsonAsync(apiError, cancellationToken);

            _logger.LogError(
                exception,
                "Erro de dominio em {Method} {Path}. StatusCode: {StatusCode}. Message: {Message}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                StatusCodes.Status400BadRequest,
                string.Join(",", erros));

            return true;

        }
    }
}
