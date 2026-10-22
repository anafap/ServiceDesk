using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Domain.Exceptions;
namespace ServiceDesk.Api.Errors;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            ArgumentException or BadHttpRequestException => 400,
            KeyNotFoundException => 404,
            StateConflictException or DbUpdateConcurrencyException => 409,
            _ => 500,
        };
        if (status == 500) logger.LogError(exception, "Request failed. TraceId {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        var detail = status switch
        {
            500 => "The request could not be completed. Try again or provide the trace ID when reporting the problem.",
            409 when exception is DbUpdateConcurrencyException => "This record changed. Refresh before trying again.",
            _ => exception.Message,
        };
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = status == 409 ? "Update conflict" : status == 500 ? "Unexpected server error" : "Request rejected", Detail = detail },
        });
    }
}
