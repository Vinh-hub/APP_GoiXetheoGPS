using System.Text.Json;
using RideAPI.Services;

namespace RideAPI.Middleware
{
    public class LocationRoutingMiddleware
    {
        private readonly RequestDelegate _next;

        public LocationRoutingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                string? region = null;

                if (context.Request.Headers.TryGetValue("X-User-Latitude", out var latHeader) ||
                    context.Request.Headers.TryGetValue("x-user-latitude", out latHeader))
                {
                    if (double.TryParse(latHeader, out var lat))
                        region = LocationRoutingService.ResolveRegionFromLatitude(lat);
                }

                if (string.IsNullOrWhiteSpace(region))
                {
                    if (context.Request.Query.TryGetValue("province", out var provinceQuery))
                        region = LocationRoutingService.ResolveRegionFromProvince(provinceQuery.ToString());
                    else if (context.Request.Query.TryGetValue("lat", out var latQuery) && double.TryParse(latQuery, out var qLat))
                        region = LocationRoutingService.ResolveRegionFromLatitude(qLat);
                }

                if (string.IsNullOrWhiteSpace(region) && (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method)))
                {
                    var contentType = context.Request.ContentType;
                    var isJson = contentType != null && contentType.Contains("application/json");
                    var isForm = contentType != null && contentType.Contains("application/x-www-form-urlencoded");

                    if (isJson || isForm)
                    {
                        context.Request.EnableBuffering();
                        try
                        {
                            if (isJson)
                            {
                                var body = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonElement>>(context.Request.Body);
                                if (body != null)
                                {
                                    if (body.TryGetValue("province", out var p) && p.ValueKind == JsonValueKind.String)
                                        region = LocationRoutingService.ResolveRegionFromProvince(p.GetString());
                                    else if (body.TryGetValue("lat", out var l) && l.TryGetDouble(out var lat))
                                        region = LocationRoutingService.ResolveRegionFromLatitude(lat);
                                }
                            }
                            else if (isForm)
                            {
                                var form = await context.Request.ReadFormAsync();
                                var province = form["province"].ToString();
                                if (!string.IsNullOrWhiteSpace(province))
                                    region = LocationRoutingService.ResolveRegionFromProvince(province);
                                else if (double.TryParse(form["lat"], out var lat))
                                    region = LocationRoutingService.ResolveRegionFromLatitude(lat);
                            }
                        }
                        catch
                        {
                        }
                        finally
                        {
                            context.Request.Body.Position = 0;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(region))
                    region = "SOUTH";

                context.Items["Region"] = region;
            }

            await _next(context);
        }
    }
}