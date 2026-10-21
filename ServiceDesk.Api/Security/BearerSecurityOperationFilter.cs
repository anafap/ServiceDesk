using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ServiceDesk.Api.Security;

public sealed class BearerSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any()) return;
        operation.Security = [new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        }];
    }
}
