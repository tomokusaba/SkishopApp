namespace AuthService.Enums;

/// <summary>
/// ユーザーアカウントのライフサイクル状態を表す列挙型。
/// アカウントのアクティベーションと停止状態の管理に使用される。
/// </summary>
/// <remarks>
/// <para>
/// 状態遷移: PendingVerification → Active ⇔ Suspended
/// </para>
/// <para>
/// PendingVerification 状態のユーザーはログインできない。
/// メール認証完了後に Active に遷移する。
/// </para>
/// <para>
/// Suspended 状態のユーザーは全ての操作がブロックされ、既存セッションも無効化される。
/// 管理者のみが Suspended ⇔ Active の遷移を実行できる。
/// </para>
/// </remarks>
public enum UserStatus
{
    /// <summary>
    /// メール認証待ち状態。ユーザー登録後、メールアドレスの認証が完了していない。
    /// この状態ではログイン不可。
    /// </summary>
    PendingVerification,

    /// <summary>
    /// アクティブ状態。メール認証が完了し、正常にサービスを利用可能。
    /// </summary>
    Active,

    /// <summary>
    /// 停止状態。管理者操作またはセキュリティポリシー違反により、アカウントが一時停止されている。
    /// 全ての操作がブロックされ、アクティブなセッションも無効化される。
    /// </summary>
    Suspended
}
