using Microsoft.AspNetCore.Diagnostics;
using SharedDomain.Exceptions;
using WebApi.Configs.Models;

namespace WebApi.Configs
{
    public class AutenticacaoExecptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is not AutenticacaoNecessariaException)
            {
                return false;
            }

            var apiError = ApiResultError.Create(exception.Message);

            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;

            await httpContext.Response
                .WriteAsJsonAsync(apiError, cancellationToken);

            return true;
        }
    }
}
