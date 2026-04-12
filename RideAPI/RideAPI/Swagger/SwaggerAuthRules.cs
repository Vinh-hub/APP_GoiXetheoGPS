using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace RideAPI.Swagger;

internal static class SwaggerAuthRules
{
    public static bool AllowsAnonymous(ControllerActionDescriptor cad)
    {
        var methodInfo = cad.MethodInfo;
        var controllerType = cad.ControllerTypeInfo.AsType();

        if (methodInfo.GetCustomAttributes(inherit: true).OfType<AllowAnonymousAttribute>().Any())
            return true;
        if (controllerType.GetCustomAttributes(inherit: true).OfType<AllowAnonymousAttribute>().Any())
            return true;

        var controllerHasAuthorize = controllerType.GetCustomAttributes(inherit: true).OfType<AuthorizeAttribute>().Any();
        var methodHasAuthorize = methodInfo.GetCustomAttributes(inherit: true).OfType<AuthorizeAttribute>().Any();

        return !controllerHasAuthorize && !methodHasAuthorize;
    }
}
