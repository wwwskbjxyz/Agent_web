namespace SProtectAgentWeb.Api.Utilities;

/// <summary>
/// Mirrors the error code definitions from the legacy Go implementation so that
/// the front-end contract remains identical.
/// </summary>
public static class ErrorCodes
{
    public const int Success = 0;

    public const int InvalidRequest = 1001;
    public const int InvalidParam = 1002;

    public const int InvalidCredentials = 2001;
    public const int TokenExpired = 2002;
    public const int TokenInvalid = 2003;
    public const int PermissionDenied = 2004;

    public const int SoftwareNotFound = 3001;
    public const int CardNotFound = 3002;
    public const int AgentNotFound = 3003;
    public const int InsufficientBalance = 3005;
    public const int DatabaseError = 9001;
    public const int InternalError = 9999;

    public static string GetMessage(int code) => code switch
    {
        Success => "操作成功",
        InvalidRequest => "无效请求",
        InvalidParam => "无效参数",
        InvalidCredentials => "用户名或密码错误",
        TokenExpired => "登录已过期",
        TokenInvalid => "无效的登录凭证",
        PermissionDenied => "权限不足",
        SoftwareNotFound => "软件位不存在",
        CardNotFound => "卡密不存在",
        AgentNotFound => "代理不存在",
        InsufficientBalance => "余额不足",
        DatabaseError => "数据库错误",
        InternalError => "内部错误",
        _ => "未知错误",
    };
}
