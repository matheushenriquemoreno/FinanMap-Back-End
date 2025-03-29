using Microsoft.AspNetCore.Diagnostics;
using WebApi.Configs.Models;

namespace WebApi.Configs
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<DomainExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<DomainExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

            var apiError = ApiResultError.Create(exception.Message);
            var statusCode = StatusCodes.Status500InternalServerError;

            if (exception.InnerException is not null)
                apiError.AddErro(exception.InnerException.Message);

            httpContext.Response.StatusCode = statusCode;

            await httpContext.Response
                .WriteAsJsonAsync(apiError, cancellationToken);

            return true;
        }
    }
}
