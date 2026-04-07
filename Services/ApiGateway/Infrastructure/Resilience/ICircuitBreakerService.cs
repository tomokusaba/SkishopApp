// ─────────────────────────────────────────────────────────────
// ICircuitBreakerService — サーキットブレーカーサービスインターフェース
//
// テスタビリティと DI 準拠のため、CircuitBreakerService の
// パブリック API を定義するインターフェース。
// ─────────────────────────────────────────────────────────────

namespace ApiGateway.Infrastructure.Resilience;

/// <summary>
/// クラスター別サーキットブレーカーの状態管理を抽象化するインターフェース。
/// </summary>
public interface ICircuitBreakerService
{
    /// <summary>
    /// 指定クラスターのサーキットが現在リクエストを許可するかを判定する。
    /// </summary>
    /// <param name="clusterId">対象クラスター ID。</param>
    /// <returns>リクエスト許可時は <c>true</c>、拒否時は <c>false</c>。</returns>
    bool AllowRequest(string clusterId);

    /// <summary>リクエスト成功を記録する。</summary>
    /// <param name="clusterId">成功したクラスター ID。</param>
    void RecordSuccess(string clusterId);

    /// <summary>リクエスト失敗を記録する。</summary>
    /// <param name="clusterId">失敗したクラスター ID。</param>
    void RecordFailure(string clusterId);

    /// <summary>指定クラスターの現在のサーキット状態を取得する。</summary>
    /// <param name="clusterId">対象クラスター ID。</param>
    /// <returns>現在の <see cref="CircuitState"/>。</returns>
    CircuitState GetState(string clusterId);
}
