using System.Net;

namespace APP_GoiXetheoGPS.Services;

public class ApiRequestException : Exception
{
    public ApiRequestException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}

public sealed class ApiReadOnlyException : ApiRequestException
{
    public const string DefaultUserMessage = "Hệ thống đang ở chế độ chỉ đọc. Vui lòng thử lại sau.";

    public ApiReadOnlyException(string? message = null)
        : base(HttpStatusCode.ServiceUnavailable, string.IsNullOrWhiteSpace(message) ? DefaultUserMessage : message)
    {
    }
}
