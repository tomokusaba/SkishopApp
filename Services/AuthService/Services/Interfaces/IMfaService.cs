using AuthService.DTOs.Responses;

namespace AuthService.Services.Interfaces;

/// <summary>
/// 多要素認証（MFA）サービスのインターフェース。
/// TOTP（時間ベースワンタイムパスワード）ベースの2要素認証の設定、検証、管理を提供します。
/// </summary>
/// <remarks>
/// セキュリティ考慮事項:
/// <list type="bullet">
///   <item>MFAシークレットは暗号化されてデータベースに保存されます</item>
///   <item>バックアップコードは一度だけ表示され、再表示できません</item>
///   <item>MFAの有効化/無効化はセキュリティログに記録されます</item>
///   <item>認証アプリ（Google Authenticator、Microsoft Authenticatorなど）と互換性があります</item>
/// </list>
/// </remarks>
public interface IMfaService
{
    /// <summary>
    /// ユーザーのMFA設定を初期化し、セットアップ情報を返します。
    /// </summary>
    /// <param name="userId">MFAを設定するユーザーのID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>シークレットキー、QRコードURI、およびバックアップコードを含むレスポンス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> が null の場合。</exception>
    /// <exception cref="Exceptions.NotFoundException">指定されたユーザーが存在しない場合。</exception>
    /// <exception cref="Exceptions.BusinessException">MFAが既に有効化されている場合。</exception>
    /// <remarks>
    /// セットアップフロー:
    /// <list type="bullet">
    ///   <item>新しいTOTPシークレットが生成されます</item>
    ///   <item>シークレットは暗号化されてデータベースに保存されます</item>
    ///   <item>QRコードURIは認証アプリでスキャン可能です</item>
    ///   <item>バックアップコードは8個生成され、このタイミングでのみ表示されます</item>
    ///   <item>MFAは <see cref="VerifyMfaAsync"/> で初回検証が成功するまで有効化されません</item>
    /// </list>
    /// </remarks>
    Task<MfaSetupResponse> SetupMfaAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// MFA認証コードを検証します。
    /// </summary>
    /// <param name="userId">検証対象のユーザーID。</param>
    /// <param name="code">6桁のTOTPコードまたはバックアップコード。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>検証が成功した場合は true、失敗した場合は false。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> または <paramref name="code"/> が null の場合。</exception>
    /// <exception cref="Exceptions.NotFoundException">ユーザーにMFAが設定されていない場合。</exception>
    /// <remarks>
    /// 検証動作:
    /// <list type="bullet">
    ///   <item>TOTPコードは前後30秒（1ステップ）のずれを許容します</item>
    ///   <item>初回検証成功時にMFAが有効化されます</item>
    ///   <item>検証結果はメトリクスとして記録されます</item>
    /// </list>
    /// </remarks>
    Task<bool> VerifyMfaAsync(string userId, string code, CancellationToken ct = default);

    /// <summary>
    /// ユーザーのMFAを無効化します。
    /// </summary>
    /// <param name="userId">MFAを無効化するユーザーのID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> が null の場合。</exception>
    /// <exception cref="Exceptions.NotFoundException">ユーザーにMFAが設定されていない場合。</exception>
    /// <remarks>
    /// セキュリティ:
    /// <list type="bullet">
    ///   <item>MFA無効化はセキュリティログに記録されます</item>
    ///   <item>無効化後も既存のシークレットデータは保持されます（再有効化に備えて）</item>
    ///   <item>この操作は適切な認証・認可の後にのみ許可してください</item>
    /// </list>
    /// </remarks>
    Task DisableMfaAsync(string userId, CancellationToken ct = default);
}
