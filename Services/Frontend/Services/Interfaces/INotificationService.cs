namespace Frontend.Services.Interfaces;

/// <summary>
/// UI 通知の抽象化インターフェース
/// H-14: Service 層から MudBlazor.ISnackbar への直接依存を除去
/// </summary>
public interface INotificationService
{
    void ShowInfo(string message);
    void ShowWarning(string message);
    void ShowError(string message);
    void ShowSuccess(string message);
}
