namespace SProtectPlatform.Api.Models.Dto;

public sealed record ApiResponse<T>(int Code, string Message, T? Data)
{
    public static ApiResponse<T> Success(T data, string message = "成功") => new(0, message, data);

    public static ApiResponse<T> Failure(string message, int code = -1) => new(code, message, default);
}

public sealed record ApiError(string Message, int Code);
