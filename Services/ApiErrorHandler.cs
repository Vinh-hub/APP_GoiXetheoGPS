using System.Net;

namespace APP_GoiXetheoGPS.Services;

public static class ApiErrorHandler
{
    public static string ToUserMessage(Exception ex)
    {
        if (ex is ApiReadOnlyException)
            return ApiReadOnlyException.DefaultUserMessage;

        if (ex is ApiRequestException apiEx)
        {
            if (apiEx.StatusCode == HttpStatusCode.Unauthorized)
                return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";

            return string.IsNullOrWhiteSpace(apiEx.Message)
                ? "Không thể xử lý yêu cầu. Vui lòng thử lại."
                : apiEx.Message;
        }

        return string.IsNullOrWhiteSpace(ex.Message)
            ? "Có lỗi xảy ra. Vui lòng thử lại."
            : ex.Message;
    }
}
