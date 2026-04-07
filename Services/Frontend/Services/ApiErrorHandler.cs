using System.Net.Http.Json;
using Frontend.Models;
using Frontend.Services.Interfaces;

namespace Frontend.Services;

/// <summary>
/// API エラーハンドリングサービス
/// §4.1 / §16.1 準拠 — 全ステータスコード（400/401/403/404/409/422/429/500/503）を処理
/// H-14: INotificationService 抽象化で MudBlazor 直接依存を除去
/// </summary>
public class ApiErrorHandler(
    INotificationService notification,
    ILogger<ApiErrorHandler> logger) : IApiErrorHandler
{
    public async Task HandleApiErrorAsync(HttpResponseMessage response, CancellationToken ct = default)
    {
        ProblemDetailsResponse? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ProblemDetails のデシリアライズに失敗");
        }

        var status = (int)response.StatusCode;
        var detail = problem?.Detail;
        var traceId = problem?.TraceId;

        switch (status)
        {
            case 400:
                if (problem?.Errors is { Count: > 0 } errors)
                {
                    throw new ApiValidationException(errors);
                }
                notification.ShowWarning(detail ?? "リクエストが不正です");
                break;

            case 401:
                throw new UnauthorizedAccessException(detail ?? "認証が必要です");

            case 403:
                notification.ShowWarning(detail ?? "この操作を行う権限がありません");
                break;

            case 404:
                notification.ShowInfo(detail ?? "リソースが見つかりません");
                break;

            case 409:
                notification.ShowWarning("データが他のユーザーにより更新されました。ページを再読込してください");
                break;

            case 422:
                notification.ShowWarning(detail ?? "処理できませんでした");
                break;

            case 429:
                var retryAfterMessage = "リクエスト数の上限に達しました。しばらくお待ちください";
                if (response.Headers.RetryAfter?.Delta is { } retryDelta)
                {
                    retryAfterMessage = $"リクエスト数の上限に達しました。{retryDelta.TotalSeconds:0}秒後に再試行してください";
                }
                notification.ShowWarning(retryAfterMessage);
                break;

            case 500:
                logger.LogError("サーバーエラー [TraceId: {TraceId}]", traceId);
                notification.ShowError("サーバーエラーが発生しました。しばらく経ってからお試しください");
                break;

            case 503:
                notification.ShowWarning("現在メンテナンス中です。しばらくお待ちください");
                break;

            default:
                logger.LogError("予期しない API エラー Status: {Status} [TraceId: {TraceId}]", status, traceId);
                notification.ShowError("サーバーエラーが発生しました。しばらく経ってからお試しください");
                break;
        }
    }

    /// <summary>
    /// ステータスコードから表示戦略を決定
    /// §16.1 準拠
    /// </summary>
    public static ErrorDisplayType GetErrorDisplayStrategy(int status) => status switch
    {
        400 => ErrorDisplayType.Inline,
        401 => ErrorDisplayType.Page,
        403 => ErrorDisplayType.Snackbar,
        404 => ErrorDisplayType.Page,
        409 => ErrorDisplayType.Dialog,
        422 => ErrorDisplayType.Snackbar,
        429 => ErrorDisplayType.Snackbar,
        _ => ErrorDisplayType.Snackbar
    };
}
