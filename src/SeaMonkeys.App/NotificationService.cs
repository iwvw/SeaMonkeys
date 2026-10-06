namespace SeaMonkeys.App;

public enum NoticeKind
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>窗口内浮层通知已停用；保留接口以便调用方继续编译。</summary>
public static class NotificationService
{
    public static void Attach(object hostPanel)
    {
    }

    public static void Info(string message) => Show(message, NoticeKind.Info);
    public static void Success(string message) => Show(message, NoticeKind.Success);
    public static void Warn(string message) => Show(message, NoticeKind.Warning);
    public static void Error(string message) => Show(message, NoticeKind.Error);

    public static void Show(string message, NoticeKind kind)
    {
    }
}
