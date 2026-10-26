using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Common;

/// Two saves raced on the same row: the second one lost, which is a failed precondition and not a server error.
public sealed class ConcurrencyExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not DbUpdateConcurrencyException) return false;
        context.Response.StatusCode = StatusCodes.Status412PreconditionFailed;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Status = StatusCodes.Status412PreconditionFailed, Detail = ETags.Changed },
        });
    }
}
