using ServiceDesk.Application.Interfaces;
namespace ServiceDesk.Api.Endpoints;

public sealed class VersionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Creation has no existing version; every edit/action must name the version being changed.
        var request = context.Arguments.OfType<IVersionedRequest>().FirstOrDefault();
        var creation = HttpMethods.IsPost(context.HttpContext.Request.Method) && !context.HttpContext.Request.RouteValues.ContainsKey("id") && !context.HttpContext.Request.RouteValues.ContainsKey("ticketId");
        if (!creation && request is not null && string.IsNullOrWhiteSpace(request.ExpectedVersion))
            throw new ArgumentException("expectedVersion is required. Refresh the record before trying again.");
        return await next(context);
    }
}
