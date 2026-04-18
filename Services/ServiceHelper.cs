using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;

namespace APP_GoiXetheoGPS.Services;

public static class ServiceHelper
{
    public static T? GetService<T>() where T : class
        => IPlatformApplication.Current?.Services.GetService<T>();
}
