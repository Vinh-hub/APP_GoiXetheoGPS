using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RideAPI.Swagger;

/// <summary>
/// Mỗi API cần đăng nhập: Swagger coi <c>Bearer</c> hoặc header <c>X-Jwt-Token</c> là một lựa chọn (OR).
/// Ổ khóa từng endpoint sẽ có ô nhập ApiKey ngay trong modal, không phụ thuộc Authorize phía trên.
/// </summary>
internal sealed class DualJwtSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor cad)
            return;

        if (SwaggerAuthRules.AllowsAnonymous(cad))
            return;

        var bearerRef = new OpenApiSecuritySchemeReference("Bearer", context.Document, string.Empty);
        var headerRef = new OpenApiSecuritySchemeReference("XJwtToken", context.Document, string.Empty);

        operation.Security =
        [
            new OpenApiSecurityRequirement { [bearerRef] = new List<string>() },
            new OpenApiSecurityRequirement { [headerRef] = new List<string>() }
        ];
    }
}
