namespace AuthService.Services.Interfaces;

/// <summary>
/// 監査ログサービスのインターフェース。
/// システム内の重要な操作を記録し、コンプライアンスとセキュリティ監査を支援します。
/// </summary>
/// <remarks>
/// セキュリティ考慮事項:
/// <list type="bullet">
///   <item>監査ログは改ざん検出のため、追記専用で設計されています</item>
///   <item>ログには個人を特定できる情報（PII）を含めないでください</item>
///   <item>ActorId は認証済みユーザーの識別子であり、未認証操作の場合は null になります</item>
/// </list>
/// </remarks>
public interface IAuditLogService
{
    /// <summary>
    /// 監査ログエントリを非同期で記録します。
    /// </summary>
    /// <param name="action">実行されたアクション（例: "CREATE", "UPDATE", "DELETE", "LOGIN"）。</param>
    /// <param name="entityType">操作対象のエンティティタイプ（例: "User", "Order", "Session"）。</param>
    /// <param name="entityId">操作対象のエンティティID。</param>
    /// <param name="actorId">操作を実行したユーザーのID。システム操作の場合は null。</param>
    /// <param name="actorRole">操作を実行したユーザーのロール（例: "ADMIN", "USER"）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="action"/>、<paramref name="entityType"/>、または <paramref name="entityId"/> が null の場合。
    /// </exception>
    /// <remarks>
    /// 監査ログは以下の目的で使用されます:
    /// <list type="bullet">
    ///   <item>セキュリティインシデントの調査</item>
    ///   <item>コンプライアンス要件への対応</item>
    ///   <item>システム操作の追跡と分析</item>
    /// </list>
    /// </remarks>
    Task LogAsync(string action, string entityType, string entityId, string? actorId, string? actorRole, CancellationToken ct = default);
}
