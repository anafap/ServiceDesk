namespace ServiceDesk.Api.Endpoints;

public sealed class DemoAccountReadOnlyFilter(IConfiguration configuration) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (configuration.GetValue("Hosting:DemoInstance", false)
            && !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "Account changes disabled",
                detail: "Account changes are disabled in this demo.",
                extensions: new Dictionary<string, object?> { ["traceId"] = context.HttpContext.TraceIdentifier });
        }

        return await next(context);
    }
}
