using AiSupportService.DTOs.Requests;
using AiSupportService.DTOs.Responses;
using AiSupportService.Exceptions;

namespace AiSupportService.Services.Interfaces;

/// <summary>
/// AI チャット機能のビジネスロジックを提供するサービスインターフェース。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、Semantic Kernel を使用した AI チャットボット機能を提供します。
/// ユーザーとの会話セッションを管理し、AI による自動応答を生成します。
/// </para>
/// <para>
/// セッションのライフサイクル:
/// <list type="number">
///   <item><description>ユーザーが新しいセッションを作成（ACTIVE 状態）</description></item>
///   <item><description>ユーザーがメッセージを送信し、AI が応答を生成</description></item>
///   <item><description>セッションがクローズまたはエスカレーションされる</description></item>
/// </list>
/// </para>
/// <para>
/// セキュリティ:
/// <list type="bullet">
///   <item><description>入力メッセージはサニタイズ処理が適用され、プロンプトインジェクションを防止します</description></item>
///   <item><description>セッションはユーザー ID で分離され、他ユーザーのセッションにはアクセスできません</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IChatService
{
    /// <summary>
    /// 新しいチャットセッションを作成する。
    /// </summary>
    /// <param name="userId">
    /// セッションを作成するユーザーの ID。
    /// </param>
    /// <param name="request">
    /// セッション作成リクエスト。タイトルを指定できます。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 作成されたセッションのレスポンス。セッション ID、タイトル、ステータスなどを含みます。
    /// </returns>
    /// <exception cref="BusinessException">
    /// ユーザーのアクティブセッション数が上限（設定値）に達している場合にスローされます。
    /// </exception>
    /// <remarks>
    /// セッション作成時に、システムプロンプトが自動的に追加されます。
    /// システムプロンプトには AI の役割と行動規範が定義されています。
    /// </remarks>
    Task<ChatSessionResponse> CreateSessionAsync(string userId, CreateChatSessionRequest request, CancellationToken ct = default);

    /// <summary>
    /// 指定セッションにメッセージを送信し、AI の応答を取得する。
    /// </summary>
    /// <param name="sessionId">
    /// メッセージを送信するセッションの ID。
    /// </param>
    /// <param name="userId">
    /// メッセージを送信するユーザーの ID。セッションの所有者である必要があります。
    /// </param>
    /// <param name="request">
    /// 送信するメッセージの内容を含むリクエスト。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// AI アシスタントの応答メッセージ。メッセージ ID、内容、トークン数などを含みます。
    /// </returns>
    /// <exception cref="NotFoundException">
    /// 指定されたセッションが存在しない、またはユーザーがアクセス権を持たない場合にスローされます。
    /// </exception>
    /// <exception cref="BusinessException">
    /// セッションがアクティブでない場合、またはメッセージ数が上限に達した場合にスローされます。
    /// </exception>
    /// <exception cref="Exceptions.SecurityException">
    /// 入力メッセージにプロンプトインジェクションが検出された場合にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// メッセージ処理の流れ:
    /// <list type="number">
    ///   <item><description>入力のサニタイズとプロンプトインジェクション検出</description></item>
    ///   <item><description>過去の会話履歴を ChatHistory に構築</description></item>
    ///   <item><description>Semantic Kernel を使用して AI 応答を生成</description></item>
    ///   <item><description>応答のフィルタリングと保存</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// AI 応答生成にはメトリクスが記録され、レスポンス時間の監視が行われます。
    /// </para>
    /// </remarks>
    Task<ChatMessageResponse> SendMessageAsync(string sessionId, string userId, SendMessageRequest request, CancellationToken ct = default);

    /// <summary>
    /// 指定ユーザーのチャットセッション一覧をページネーション付きで取得する。
    /// </summary>
    /// <param name="userId">
    /// セッション一覧を取得するユーザーの ID。
    /// </param>
    /// <param name="page">
    /// ページ番号（1 始まり）。デフォルトは 1。
    /// </param>
    /// <param name="pageSize">
    /// 1 ページあたりの件数。デフォルトは 20。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// ページネーション付きセッションレスポンス。総件数、現在ページ、総ページ数を含みます。
    /// </returns>
    /// <remarks>
    /// セッションは作成日時の降順（新しい順）でソートされます。
    /// </remarks>
    Task<PagedResult<ChatSessionResponse>> GetSessionsAsync(string userId, int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>
    /// 指定セッションの詳細を取得する。
    /// </summary>
    /// <param name="sessionId">
    /// 取得するセッションの ID。
    /// </param>
    /// <param name="userId">
    /// セッションにアクセスするユーザーの ID。セッションの所有者である必要があります。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// セッションレスポンス。セッションが存在しない、またはアクセス権がない場合は <c>null</c>。
    /// </returns>
    Task<ChatSessionResponse?> GetSessionByIdAsync(string sessionId, string userId, CancellationToken ct = default);

    /// <summary>
    /// 指定セッションのメッセージ履歴をページネーション付きで取得する。
    /// </summary>
    /// <param name="sessionId">
    /// メッセージを取得するセッションの ID。
    /// </param>
    /// <param name="userId">
    /// セッションにアクセスするユーザーの ID。セッションの所有者である必要があります。
    /// </param>
    /// <param name="page">
    /// ページ番号（1 始まり）。デフォルトは 1。
    /// </param>
    /// <param name="pageSize">
    /// 1 ページあたりの件数。デフォルトは 50。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// ページネーション付きメッセージレスポンス。システムメッセージは除外されます。
    /// </returns>
    /// <exception cref="NotFoundException">
    /// 指定されたセッションが存在しない、またはユーザーがアクセス権を持たない場合にスローされます。
    /// </exception>
    /// <remarks>
    /// メッセージは作成日時の昇順（古い順）でソートされます。
    /// 内部的に使用されるシステムメッセージ（role = "system"）は結果から除外されます。
    /// </remarks>
    Task<PagedResult<ChatMessageResponse>> GetMessagesAsync(string sessionId, string userId, int page = 1, int pageSize = 50, CancellationToken ct = default);

    /// <summary>
    /// 指定セッションをクローズする。
    /// </summary>
    /// <param name="sessionId">
    /// クローズするセッションの ID。
    /// </param>
    /// <param name="userId">
    /// セッションをクローズするユーザーの ID。セッションの所有者である必要があります。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// クローズ操作を表すタスク。
    /// </returns>
    /// <exception cref="NotFoundException">
    /// 指定されたセッションが存在しない、またはユーザーがアクセス権を持たない場合にスローされます。
    /// </exception>
    /// <exception cref="ConcurrencyException">
    /// 楽観的ロックの競合が発生した場合にスローされます。
    /// </exception>
    /// <remarks>
    /// クローズされたセッションは CLOSED 状態になり、新しいメッセージを送信できなくなります。
    /// </remarks>
    Task CloseSessionAsync(string sessionId, string userId, CancellationToken ct = default);

    /// <summary>
    /// 指定セッションを有人対応にエスカレーションする。
    /// </summary>
    /// <param name="sessionId">
    /// エスカレーションするセッションの ID。
    /// </param>
    /// <param name="userId">
    /// セッションをエスカレーションするユーザーの ID。セッションの所有者である必要があります。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// エスカレーション操作を表すタスク。
    /// </returns>
    /// <exception cref="NotFoundException">
    /// 指定されたセッションが存在しない、またはユーザーがアクセス権を持たない場合にスローされます。
    /// </exception>
    /// <exception cref="ConcurrencyException">
    /// 楽観的ロックの競合が発生した場合にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// エスカレーションされたセッションは ESCALATED 状態になります。
    /// この操作により、<c>ai.chat.escalated</c> トピックにイベントが発行され、
    /// カスタマーサポートチームに通知されます。
    /// </para>
    /// <para>
    /// イベントには以下の情報が含まれます:
    /// <list type="bullet">
    ///   <item><description>セッション ID</description></item>
    ///   <item><description>ユーザー ID</description></item>
    ///   <item><description>セッションの要約（タイトル）</description></item>
    ///   <item><description>発生日時</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    Task EscalateSessionAsync(string sessionId, string userId, CancellationToken ct = default);
}
