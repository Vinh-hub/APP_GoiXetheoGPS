using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RideAPI.Swagger;

/// <summary>Security Bearer toàn tài liệu — Swagger UI (kể cả modal ổ khóa từng endpoint) cần để resolve scheme.</summary>
internal sealed class BearerDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swagger, DocumentFilterContext context)
    {
        var schemeRef = new OpenApiSecuritySchemeReference("Bearer", swagger, string.Empty);
        swagger.Security ??= new List<OpenApiSecurityRequirement>();
        swagger.Security.Add(new OpenApiSecurityRequirement { [schemeRef] = new List<string>() });
    }
}
