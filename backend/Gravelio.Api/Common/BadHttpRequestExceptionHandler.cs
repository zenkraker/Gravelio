using Microsoft.AspNetCore.Diagnostics;

namespace Gravelio.Api.Common;

/// Kestrel reports a client mistake (a body over its limit, a malformed request) as a <see cref="BadHttpRequestException"/> carrying
/// the right 4xx. Without this, the exception middleware would turn it into a 500.
public sealed class BadHttpRequestExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not BadHttpRequestException bad) return false;
        context.Response.StatusCode = bad.StatusCode;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Status = bad.StatusCode },
        });
    }
}
