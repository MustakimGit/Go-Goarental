using GoGoaRentals.Services.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GoGoaRentals.Server.Infrastructure;

/// <summary>Turns expected business failures from the services into ProblemDetails responses.</summary>
public sealed class ServiceExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ServiceException serviceException)
        {
            return false;
        }

        httpContext.Response.StatusCode = serviceException switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity,
        };

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = httpContext.Response.StatusCode,
                Title = serviceException.Title,
                Detail = serviceException.Message,
            },
            Exception = exception,
        });
    }
}
