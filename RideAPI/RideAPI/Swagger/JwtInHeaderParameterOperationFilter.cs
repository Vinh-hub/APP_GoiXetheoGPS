using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RideAPI.Swagger;

/// <summary>Thêm ô header <c>X-Jwt-Token</c> ngay trong Try it out (Swagger), tương đương Bearer khi nhập token.</summary>
internal sealed class JwtInHeaderParameterOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor cad)
            return;

        if (SwaggerAuthRules.AllowsAnonymous(cad))
            return;

        operation.Parameters ??= new List<IOpenApiParameter>();
        if (operation.Parameters.Any(p => string.Equals(p.Name, "X-Jwt-Token", StringComparison.OrdinalIgnoreCase)))
            return;

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Jwt-Token",
            In = ParameterLocation.Header,
            Required = false,
            Description =
                "Dán JWT từ POST /api/auth/login (chỉ chuỗi token). Cùng ý nghĩa với Bearer; có thể nhập ở đây hoặc trong modal ổ khóa (XJwtToken).",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
    }
}
