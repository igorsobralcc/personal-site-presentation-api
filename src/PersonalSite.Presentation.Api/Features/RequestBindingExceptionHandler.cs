using Microsoft.AspNetCore.Diagnostics;
using PersonalSite.Presentation.Api.Common;

namespace PersonalSite.Presentation.Api.Features;

public sealed class RequestBindingExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest })
        {
            return false;
        }
        await ApiProblems.Validation(httpContext, new()
        {
            ["document"] = ["The request body is invalid or contains an invalid field type."]
        }).ExecuteAsync(httpContext);
        return true;
    }
}
