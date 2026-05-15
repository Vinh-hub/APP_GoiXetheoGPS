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
    public const string DefaultUserMessage = "Không ghi được dữ liệu: không mở được kết nối tới PostgreSQL primary (master có thể đang tắt). Thử lại sau khi bật container master.";

    public ApiReadOnlyException(string? message = null)
        : base(HttpStatusCode.ServiceUnavailable, string.IsNullOrWhiteSpace(message) ? DefaultUserMessage : message)
    {
    }
}
