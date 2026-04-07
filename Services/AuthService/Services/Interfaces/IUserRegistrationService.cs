using AuthService.DTOs.Requests;
using AuthService.DTOs.Responses;

namespace AuthService.Services.Interfaces;

/// <summary>
/// ユーザー登録サービスのインターフェース。
/// ユーザーの登録、メール認証、アカウント削除機能を提供します。
/// </summary>
/// <remarks>
/// セキュリティ考慮事項:
/// <list type="bullet">
///   <item>パスワードはASP.NET Core Identityのハッシュ関数でハッシュ化されます</item>
///   <item>メール認証が完了するまでアカウントは有効化されません</item>
///   <item>メールアドレスとユーザー名の重複チェックが行われます</item>
///   <item>すべての登録・削除操作はセキュリティログに記録されます</item>
///   <item>ハード削除時でもユーザーIDは保持され、個人情報のみ匿名化されます</item>
/// </list>
/// </remarks>
public interface IUserRegistrationService
{
    /// <summary>
    /// 新規ユーザーを登録します。
    /// </summary>
    /// <param name="request">ユーザー登録情報を含むリクエスト。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>登録されたユーザー情報を含むレスポンス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> が null の場合。</exception>
    /// <exception cref="Exceptions.BusinessException">
    /// メールアドレスまたはユーザー名が既に使用されている場合。
    /// </exception>
    /// <remarks>
    /// 登録プロセス:
    /// <list type="bullet">
    ///   <item>メールアドレスとユーザー名の重複チェック</item>
    ///   <item>パスワードのハッシュ化</item>
    ///   <item>初期状態は "PendingVerification"（メール認証待ち）</item>
    ///   <item>デフォルトロール "USER" の割り当て</item>
    ///   <item>メール認証トークンの発行（24時間有効）</item>
    ///   <item>登録イベントとメール認証イベントのOutboxへの記録</item>
    /// </list>
    /// </remarks>
    Task<UserResponse> RegisterAsync(UserCreateRequest request, CancellationToken ct = default);

    /// <summary>
    /// ユーザーを論理削除（無効化）します。
    /// </summary>
    /// <param name="userId">削除するユーザーのID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> が null の場合。</exception>
    /// <exception cref="Exceptions.NotFoundException">指定されたユーザーが存在しない場合。</exception>
    /// <remarks>
    /// 論理削除:
    /// <list type="bullet">
    ///   <item>IsActive を false に設定します</item>
    ///   <item>Status を "Suspended" に変更します</item>
    ///   <item>ユーザーデータは保持されます</item>
    ///   <item>セキュリティイベント "USER_SOFT_DELETED" が記録されます</item>
    ///   <item>復元可能です</item>
    /// </list>
    /// </remarks>
    Task SoftDeleteAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// ユーザーを物理削除（個人情報の匿名化）します。
    /// </summary>
    /// <param name="userId">削除するユーザーのID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> が null の場合。</exception>
    /// <exception cref="Exceptions.NotFoundException">指定されたユーザーが存在しない場合。</exception>
    /// <remarks>
    /// 物理削除（GDPR対応）:
    /// <list type="bullet">
    ///   <item>メールアドレスは "deleted_{userId}@deleted.local" に変更されます</item>
    ///   <item>ユーザー名、パスワードハッシュ、氏名は null に設定されます</item>
    ///   <item>ユーザーIDは監査証跡のために保持されます</item>
    ///   <item>セキュリティイベント "USER_HARD_DELETED" が記録されます</item>
    ///   <item>この操作は元に戻せません</item>
    /// </list>
    /// </remarks>
    Task HardDeleteAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// メール認証トークンを検証し、ユーザーのメールアドレスを認証済みにします。
    /// </summary>
    /// <param name="token">メール認証トークン。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> が null の場合。</exception>
    /// <exception cref="Exceptions.BusinessException">
    /// トークンが無効、トークンタイプが不正、既に使用済み、または期限切れの場合。
    /// </exception>
    /// <exception cref="Exceptions.NotFoundException">トークンに関連付けられたユーザーが見つからない場合。</exception>
    /// <remarks>
    /// 認証完了時:
    /// <list type="bullet">
    ///   <item>IsEmailVerified が true に設定されます</item>
    ///   <item>Status が "Active" に変更されます</item>
    ///   <item>トークンは使用済みとしてマークされます</item>
    ///   <item>セキュリティイベント "EMAIL_VERIFIED" が記録されます</item>
    /// </list>
    /// </remarks>
    Task VerifyEmailAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// メール認証トークンを再発行し、認証メールを再送信します。
    /// </summary>
    /// <param name="userId">再送信対象のユーザーID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> が null の場合。</exception>
    /// <exception cref="Exceptions.NotFoundException">指定されたユーザーが存在しない場合。</exception>
    /// <exception cref="Exceptions.BusinessException">メールアドレスが既に認証済みの場合。</exception>
    /// <remarks>
    /// 再送信プロセス:
    /// <list type="bullet">
    ///   <item>既存の認証トークンは無効化されます</item>
    ///   <item>新しいトークン（24時間有効）が発行されます</item>
    ///   <item>メール認証イベントがOutboxに記録されます</item>
    /// </list>
    /// </remarks>
    Task ResendVerificationEmailAsync(string userId, CancellationToken ct = default);
}
