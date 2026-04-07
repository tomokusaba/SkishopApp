namespace Frontend.Services.Interfaces;

/// <summary>
/// API エラーハンドリングサービスインターフェース
/// §4.1 / §16.1 準拠 — 全ステータスコード（400/401/403/404/409/422/429/500/503）を処理
/// </summary>
public interface IApiErrorHandler
{
    Task HandleApiErrorAsync(HttpResponseMessage response, CancellationToken ct = default);
}
