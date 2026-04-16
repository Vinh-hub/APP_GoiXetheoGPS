using System.Text.Json;

namespace RideAPI.Middleware
{
    public class LocationRoutingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly HashSet<string> NorthernProvinces = new()
        {
            "Hà Nội", "Hải Phòng", "Quảng Ninh", "Bắc Ninh", "Hải Dương", "Hưng Yên",
            "Thái Bình", "Hà Nam", "Nam Định", "Ninh Bình", "Lào Cai", "Yên Bái",
            "Điện Biên", "Lai Châu", "Sơn La", "Hòa Bình", "Thái Nguyên", "Lạng Sơn",
            "Bắc Giang", "Phú Thọ", "Vĩnh Phúc", "Tuyên Quang", "Hà Giang", "Cao Bằng", "Bắc Kạn"
        };

        public LocationRoutingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                string region = null;

                // 1. Header X-User-Latitude (không phân biệt hoa thường)
                if (context.Request.Headers.TryGetValue("X-User-Latitude", out var latHeader) ||
                    context.Request.Headers.TryGetValue("x-user-latitude", out latHeader))
                {
                    if (double.TryParse(latHeader, out var lat))
                        region = lat > 16 ? "NORTH" : "SOUTH";
                }

                // 2. Nếu chưa có, kiểm tra query string (GET)
                if (string.IsNullOrEmpty(region) && context.Request.Method == "GET")
                {
                    var province = context.Request.Query["province"].ToString();
                    if (!string.IsNullOrEmpty(province))
                        region = NorthernProvinces.Contains(province) ? "NORTH" : "SOUTH";
                    else if (context.Request.Query.TryGetValue("lat", out var latQuery) && double.TryParse(latQuery, out var lat))
                        region = lat > 16 ? "NORTH" : "SOUTH";
                }

                // 3. Nếu chưa có, kiểm tra body (POST/PUT) - chỉ xử lý khi có content type JSON hoặc form
                if (string.IsNullOrEmpty(region) &&
                    (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method)))
                {
                    // Chỉ xử lý nếu content type là JSON hoặc form
                    var contentType = context.Request.ContentType;
                    bool isJson = contentType != null && contentType.Contains("application/json");
                    bool isForm = contentType != null && contentType.Contains("application/x-www-form-urlencoded");

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
                                    {
                                        var province = p.GetString();
                                        if (!string.IsNullOrWhiteSpace(province))
                                            region = NorthernProvinces.Contains(province) ? "NORTH" : "SOUTH";
                                    }
                                    else if (body.TryGetValue("lat", out var l))
                                    {
                                        double lat;
                                        if ((l.ValueKind == JsonValueKind.Number && l.TryGetDouble(out lat)) ||
                                            (l.ValueKind == JsonValueKind.String && double.TryParse(l.GetString(), out lat)))
                                        {
                                            region = lat > 16 ? "NORTH" : "SOUTH";
                                        }
                                    }
                                }
                            }
                            else if (isForm)
                            {
                                var form = await context.Request.ReadFormAsync();
                                var province = form["province"].ToString();
                                if (!string.IsNullOrWhiteSpace(province))
                                    region = NorthernProvinces.Contains(province) ? "NORTH" : "SOUTH";
                                else if (double.TryParse(form["lat"], out var lat))
                                    region = lat > 16 ? "NORTH" : "SOUTH";
                            }
                        }
                        catch (Exception)
                        {
                            // Bỏ qua lỗi khi không thể đọc body (ví dụ body rỗng hoặc malformed)
                        }
                        finally
                        {
                            context.Request.Body.Position = 0;
                        }
                    }
                }

                if (string.IsNullOrEmpty(region))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Missing location: provide province or lat" });
                    return;
                }

                context.Items["Region"] = region;
            }

            await _next(context);
        }
    }
}