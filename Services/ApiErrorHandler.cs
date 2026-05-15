using System.Net;

namespace APP_GoiXetheoGPS.Services;

public static class ApiErrorHandler
{
    public static string ToUserMessage(Exception ex)
    {
        if (TryNetworkDisconnectMessage(ex) is { } netMsg)
            return netMsg;

        if (ex is ApiReadOnlyException)
            return ApiReadOnlyException.DefaultUserMessage;

        if (ex is ApiRequestException apiEx)
        {
            if (apiEx.StatusCode == HttpStatusCode.Unauthorized)
                return string.IsNullOrWhiteSpace(apiEx.Message)
                    ? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
                    : apiEx.Message;

            return string.IsNullOrWhiteSpace(apiEx.Message)
                ? "Không thể xử lý yêu cầu. Vui lòng thử lại."
                : apiEx.Message;
        }

        return string.IsNullOrWhiteSpace(ex.Message)
            ? "Có lỗi xảy ra. Vui lòng thử lại."
            : ex.Message;
    }

    /// <summary>HttpClient / Android hay báo "Socket closed" khi server đóng kết nối hoặc API sập giữa chừng.</summary>
    static string? TryNetworkDisconnectMessage(Exception ex)
    {
        for (var c = ex; c is not null; c = c.InnerException)
        {
            var m = c.Message;
            if (string.IsNullOrWhiteSpace(m))
                continue;

            if (m.Contains("Socket closed", StringComparison.OrdinalIgnoreCase)
                || m.Contains("Connection reset", StringComparison.OrdinalIgnoreCase)
                || m.Contains("broken pipe", StringComparison.OrdinalIgnoreCase))
            {
                return "Mất kết nối tới máy chủ (kết nối bị đóng). Hãy kiểm tra RideAPI có đang chạy, URL trong cấu hình app, và mạng của emulator/thiết bị rồi thử lại.";
            }
        }

        return null;
    }
}
