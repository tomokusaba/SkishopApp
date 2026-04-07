namespace MailSendService.Services.Interfaces;

/// <summary>
/// GDPR（EU 一般データ保護規則）準拠の操作を提供するサービスインターフェース。
/// </summary>
/// <remarks>
/// <para>P2-2: MailService からの責務分割。</para>
/// <para>以下の GDPR 権利に対応する:</para>
/// <list type="bullet">
/// <item><description>第 17 条: 消去権（忘れられる権利）</description></item>
/// <item><description>第 18 条: 処理の制限を求める権利</description></item>
/// <item><description>第 21 条: 異議を唱える権利（ダイレクトマーケティングの拒否）</description></item>
/// </list>
/// </remarks>
public interface IGdprComplianceService
{
    /// <summary>
    /// GDPR 同意撤回イベントを処理し、該当ユーザーへのメール送信を抑制する。
    /// </summary>
    /// <param name="userId">同意を撤回したユーザーの ID。</param>
    /// <param name="consentType">撤回された同意の種別（例: "marketing"）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    /// <remarks>
    /// consentType が "marketing" の場合のみ、抑制レコードを追加する。
    /// 既に抑制されている場合は重複登録しない。
    /// </remarks>
    Task ProcessConsentRevokedAsync(string userId, string consentType, CancellationToken ct = default);

    /// <summary>
    /// GDPR ユーザー削除イベントを処理し、関連する個人情報を匿名化する（第 17 条対応）。
    /// </summary>
    /// <param name="userId">削除対象のユーザー ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    /// <remarks>
    /// <para>メールアドレスを SHA-256 ハッシュベースの匿名アドレスに置換する。</para>
    /// <para>未送信（Pending）のメールは Skipped ステータスに変更する。</para>
    /// <para>元のメールアドレスに紐づく抑制レコードも削除する。</para>
    /// </remarks>
    Task ProcessUserDeletedAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// GDPR データ処理制限イベントを処理し、該当ユーザーへのメール送信を一時停止する（第 18 条対応）。
    /// </summary>
    /// <param name="userId">処理を制限するユーザーの ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    /// <remarks>
    /// 抑制理由として <c>ProcessingRestricted</c> を設定する。
    /// 既に抑制されている場合は重複登録しない。
    /// </remarks>
    Task ProcessUserProcessingRestrictedAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// GDPR データ処理制限の解除イベントを処理し、該当ユーザーへのメール送信を再開する。
    /// </summary>
    /// <param name="userId">処理制限を解除するユーザーの ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    /// <remarks>
    /// 抑制理由 <c>ProcessingRestricted</c> のレコードを削除する。
    /// </remarks>
    Task ProcessUserProcessingUnrestrictedAsync(string userId, CancellationToken ct = default);
}
