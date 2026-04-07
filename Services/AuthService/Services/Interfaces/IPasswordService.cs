namespace AuthService.Services.Interfaces;

/// <summary>
/// パスワード管理サービスのインターフェース。
/// パスワードリセット、変更、および履歴管理を提供します。
/// </summary>
/// <remarks>
/// セキュリティ考慮事項:
/// <list type="bullet">
///   <item>パスワードリセットトークンは24時間で期限切れになります</item>
///   <item>パスワード履歴機能により、直近5つのパスワードの再利用を防止します</item>
///   <item>パスワードはASP.NET Core Identityのハッシュ関数（PBKDF2）でハッシュ化されます</item>
///   <item>すべてのパスワード関連操作はセキュリティログに記録されます</item>
///   <item>存在しないメールアドレスへのリセット要求でもタイミング攻撃を防ぐため同一レスポンスを返します</item>
/// </list>
/// </remarks>
public interface IPasswordService
{
    /// <summary>
    /// パスワードリセットを要求し、リセットトークンを発行します。
    /// </summary>
    /// <param name="email">パスワードをリセットするユーザーのメールアドレス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="email"/> が null の場合。</exception>
    /// <remarks>
    /// セキュリティ:
    /// <list type="bullet">
    ///   <item>存在しないメールアドレスでも正常に完了したように見せます（タイミング攻撃対策）</item>
    ///   <item>既存のリセットトークンは無効化されます</item>
    ///   <item>リセット要求はOutboxイベントとして記録され、メール送信サービスに連携されます</item>
    ///   <item>トークンは24時間有効です</item>
    /// </list>
    /// </remarks>
    Task RequestResetAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// リセットトークンを検証し、新しいパスワードを設定します。
    /// </summary>
    /// <param name="token">パスワードリセットトークン。</param>
    /// <param name="newPassword">新しいパスワード。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> または <paramref name="newPassword"/> が null の場合。</exception>
    /// <exception cref="Exceptions.BusinessException">
    /// トークンが無効、既に使用済み、期限切れの場合、
    /// または新しいパスワードが過去に使用されたものと同じ場合。
    /// </exception>
    /// <exception cref="Exceptions.NotFoundException">トークンに関連付けられたユーザーが見つからない場合。</exception>
    /// <remarks>
    /// セキュリティ:
    /// <list type="bullet">
    ///   <item>トークンは一度だけ使用可能です</item>
    ///   <item>過去5つのパスワードとの重複チェックが行われます</item>
    ///   <item>新しいパスワードはハッシュ化されてパスワード履歴に追加されます</item>
    ///   <item>パスワード変更イベントがOutboxに記録されます</item>
    /// </list>
    /// </remarks>
    Task ConfirmResetAsync(string token, string newPassword, CancellationToken ct = default);

    /// <summary>
    /// 認証済みユーザーのパスワードを変更します。
    /// </summary>
    /// <param name="userId">パスワードを変更するユーザーのID。</param>
    /// <param name="currentPassword">現在のパスワード。</param>
    /// <param name="newPassword">新しいパスワード。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException">いずれかのパラメータが null の場合。</exception>
    /// <exception cref="Exceptions.NotFoundException">指定されたユーザーが存在しない場合。</exception>
    /// <exception cref="Exceptions.BusinessException">
    /// 現在のパスワードが正しくない場合、
    /// または新しいパスワードが過去に使用されたものと同じ場合。
    /// </exception>
    /// <remarks>
    /// セキュリティ:
    /// <list type="bullet">
    ///   <item>現在のパスワードの検証が必須です</item>
    ///   <item>過去5つのパスワードとの重複チェックが行われます</item>
    ///   <item>パスワード変更の失敗はセキュリティログに記録されます</item>
    ///   <item>パスワード変更イベントがOutboxに記録されます</item>
    /// </list>
    /// </remarks>
    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default);
}
