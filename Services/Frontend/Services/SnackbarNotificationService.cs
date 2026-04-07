using Frontend.Services.Interfaces;
using MudBlazor;

namespace Frontend.Services;

/// <summary>
/// MudBlazor ISnackbar を使用した通知サービス実装
/// H-14: UI 依存を Service 層から分離
/// </summary>
public class SnackbarNotificationService(ISnackbar snackbar) : INotificationService
{
    public void ShowInfo(string message) => snackbar.Add(message, Severity.Info);
    public void ShowWarning(string message) => snackbar.Add(message, Severity.Warning);
    public void ShowError(string message) => snackbar.Add(message, Severity.Error);
    public void ShowSuccess(string message) => snackbar.Add(message, Severity.Success);
}
