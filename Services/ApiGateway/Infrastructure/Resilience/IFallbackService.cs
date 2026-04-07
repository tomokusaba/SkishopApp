// ─────────────────────────────────────────────────────────────
// IFallbackService — フォールバックサービスインターフェース
//
// サーキットブレーカー Open 時のフォールバック戦略を
// 抽象化するインターフェース。テスタビリティ向上のため定義。
// ─────────────────────────────────────────────────────────────

namespace ApiGateway.Infrastructure.Resilience;

/// <summary>
/// サーキットブレーカー Open 時のフォールバックレスポンス生成を抽象化するインターフェース。
/// </summary>
public interface IFallbackService
{
    /// <summary>
    /// 指定クラスターのフォールバックレスポンスを生成する。
    /// </summary>
    /// <param name="clusterId">障害が発生したクラスター ID。</param>
    /// <param name="requestPath">元のリクエストパス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>フォールバックレスポンス。フォールバック不可の場合は <c>null</c>。</returns>
    Task<FallbackResponse?> GetFallbackAsync(
        string clusterId, string requestPath, CancellationToken ct = default);

    /// <summary>
    /// バックエンドの成功レスポンスをフォールバックキャッシュに書き込む。
    /// </summary>
    /// <param name="clusterId">クラスター ID。</param>
    /// <param name="requestPath">リクエストパス。</param>
    /// <param name="responseBody">レスポンスボディ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task CacheResponseAsync(
        string clusterId, string requestPath, string responseBody, CancellationToken ct = default);
}
