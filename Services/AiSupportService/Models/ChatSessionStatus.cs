namespace AiSupportService.Models;

/// <summary>
/// チャットセッションの状態を定義する列挙型。
/// </summary>
/// <remarks>
/// <para>
/// この列挙型は <see cref="ChatSession"/> のライフサイクル状態を表現します。
/// データベースには大文字の文字列形式（例: <c>"ACTIVE"</c>）で格納されます。
/// </para>
/// <para>
/// 状態遷移:
/// <list type="bullet">
///   <item><description><see cref="Active"/> → <see cref="Closed"/>: ユーザーまたはシステムが会話を終了</description></item>
///   <item><description><see cref="Active"/> → <see cref="Escalated"/>: AI では対応困難な問題でオペレーターへ転送</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// if (session.StatusEnum == ChatSessionStatus.Active)
/// {
///     session.AddMessage("user", "ありがとうございました");
///     session.Close();
/// }
/// </code>
/// </example>
public enum ChatSessionStatus
{
    /// <summary>
    /// アクティブ状態（会話中）。
    /// </summary>
    /// <remarks>
    /// セッション作成時のデフォルト状態です。
    /// この状態でのみメッセージを追加できます。
    /// </remarks>
    Active,

    /// <summary>
    /// クローズ済み状態。
    /// </summary>
    /// <remarks>
    /// 会話が正常に終了した状態です。
    /// この状態のセッションにはメッセージを追加できません。
    /// </remarks>
    Closed,

    /// <summary>
    /// オペレーターへエスカレーション済み状態。
    /// </summary>
    /// <remarks>
    /// <para>
    /// AI アシスタントでは対応が困難と判断された場合に、
    /// 人間のオペレーターへ転送された状態です。
    /// </para>
    /// <para>
    /// エスカレーション理由の例:
    /// <list type="bullet">
    ///   <item><description>クレームや苦情への対応</description></item>
    ///   <item><description>複雑な注文変更や返品処理</description></item>
    ///   <item><description>AI の回答に対するユーザーの不満</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    Escalated
}
