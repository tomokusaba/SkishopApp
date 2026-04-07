using AuthService.Models;
using AuthService.Repositories.Interfaces;
using AuthService.Services.Interfaces;

namespace AuthService.Services;

/// <summary>
/// <see cref="IAuditLogService"/> の実装クラス。
/// システム内の重要な操作を監査ログとして記録します。
/// </summary>
/// <remarks>
/// セキュリティ考慮事項:
/// <list type="bullet">
///   <item>監査ログはコンプライアンス要件に対応するため、改ざん防止措置を講じてください</item>
///   <item>ログにはPII（個人を特定できる情報）を含めないでください</item>
///   <item>サービス名は固定で "AuthService" が設定されます</item>
/// </list>
/// </remarks>
/// <param name="auditLogRepository">監査ログリポジトリ。</param>
/// <param name="timeProvider">時刻プロバイダー（テスト可能性のためDI経由で注入）。</param>
/// <param name="logger">ロガー。</param>
public class AuditLogService(
    IAuditLogRepository auditLogRepository,
    TimeProvider timeProvider,
    ILogger<AuditLogService> logger) : IAuditLogService
{
    /// <inheritdoc />
    /// <remarks>
    /// 監査ログには以下の情報が記録されます:
    /// <list type="bullet">
    ///   <item>ServiceName: 常に "AuthService"</item>
    ///   <item>EntityType, EntityId: 操作対象のエンティティ情報</item>
    ///   <item>Action: 実行された操作</item>
    ///   <item>ActorId, ActorRole: 操作実行者の情報</item>
    ///   <item>CreatedAt: UTC時刻での記録日時</item>
    /// </list>
    /// </remarks>
    public async Task LogAsync(
        string action,
        string entityType,
        string entityId,
        string? actorId,
        string? actorRole,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(entityId);

        var auditLog = new AuditLog
        {
            ServiceName = "AuthService",
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorId = actorId,
            ActorRole = actorRole,
            CreatedAt = timeProvider.GetUtcNow()
        };

        await auditLogRepository.AddAsync(auditLog, ct);
        await auditLogRepository.SaveChangesAsync(ct);

        logger.LogInformation(
            "監査ログ記録: Action={Action}, EntityType={EntityType}, EntityId={EntityId}, ActorId={ActorId}",
            action, entityType, entityId, actorId);
    }
}
