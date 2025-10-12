namespace SProtectAgentWeb.Api.Utilities;

public record ApiResponse<T>(int Code, string Message, T? Data)
{
    public static ApiResponse<T> FromError(int code, string? message = null) =>
        new(code, message ?? ErrorCodes.GetMessage(code), default);

    public static ApiResponse<T> FromSuccess(T? data, string message = "操作成功") =>
        new(ErrorCodes.Success, message, data);
}

public static class ApiResponse
{
    public static ApiResponse<object?> Success(string message = "操作成功") =>
        ApiResponse<object?>.FromSuccess(null, message);

    public static ApiResponse<T> Success<T>(T data, string message = "操作成功") =>
        ApiResponse<T>.FromSuccess(data, message);

    public static ApiResponse<object?> Error(int code, string? message = null) =>
        ApiResponse<object?>.FromError(code, message);
}
