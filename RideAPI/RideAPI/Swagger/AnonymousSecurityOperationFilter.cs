using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RideAPI.Swagger;

/// <summary>
/// Khi có security toàn cục (Bearer), mọi operation mặc định đều “cần JWT”.
/// Ghi đè <c>security: []</c> cho các endpoint ASP.NET cho phép anonymous (login, nearby, …).
/// </summary>
internal sealed class AnonymousSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor cad)
            return;

        if (SwaggerAuthRules.AllowsAnonymous(cad))
            operation.Security = new List<OpenApiSecurityRequirement>();
    }
}
