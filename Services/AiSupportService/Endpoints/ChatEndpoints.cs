using System.Security.Claims;
using AiSupportService.DTOs.Requests;
using AiSupportService.DTOs.Responses;
using AiSupportService.Exceptions;
using AiSupportService.Services.Interfaces;
using FluentValidation;

namespace AiSupportService.Endpoints;

/// <summary>
/// チャット機能の Minimal API エンドポイントを定義する。
/// </summary>
/// <remarks>
/// <para>
/// このクラスは AI チャットボットとのセッション管理およびメッセージ送受信機能を提供します。
/// ユーザーは新規セッションの作成、メッセージの送信、セッション履歴の取得が可能です。
/// また、有人サポートへのエスカレーション機能も提供します。
/// </para>
/// <para>
/// <b>Base path:</b> /api/v1/ai/chat
/// </para>
/// <para>
/// <b>Authentication:</b> Required (全エンドポイント)
/// </para>
/// <para>
/// <b>Rate Limiting:</b> chat-api ポリシー適用
/// </para>
/// </remarks>
public static class ChatEndpoints
{
    /// <summary>
    /// チャット関連のエンドポイントをルートビルダーに登録する。
    /// </summary>
    /// <param name="app">エンドポイントを登録する <see cref="IEndpointRouteBuilder"/>。</param>
    /// <remarks>
    /// <para>登録されるエンドポイント:</para>
    /// <list type="bullet">
    ///   <item>
    ///     <term>POST /sessions</term>
    ///     <description>新規チャットセッションを作成</description>
    ///   </item>
    ///   <item>
    ///     <term>POST /sessions/{sessionId}/messages</term>
    ///     <description>セッションにメッセージを送信し、AI からの応答を取得</description>
    ///   </item>
    ///   <item>
    ///     <term>GET /sessions</term>
    ///     <description>ユーザーの全セッション一覧を取得</description>
    ///   </item>
    ///   <item>
    ///     <term>GET /sessions/{sessionId}</term>
    ///     <description>特定セッションの詳細を取得</description>
    ///   </item>
    ///   <item>
    ///     <term>GET /sessions/{sessionId}/messages</term>
    ///     <description>セッション内のメッセージ履歴を取得</description>
    ///   </item>
    ///   <item>
    ///     <term>POST /sessions/{sessionId}/close</term>
    ///     <description>セッションを終了</description>
    ///   </item>
    ///   <item>
    ///     <term>POST /sessions/{sessionId}/escalate</term>
    ///     <description>有人サポートにエスカレーション</description>
    ///   </item>
    /// </list>
    /// </remarks>
    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/chat")
            .RequireRateLimiting("chat-api")
            .WithTags("Chat")
            .RequireAuthorization();

        // POST /api/v1/ai/chat/sessions
        // 新規チャットセッションを作成する（§2.1 画面 19: 認証不要*）。
        // 未ログイン時はゲスト ID でセッションを作成する。
        // Request: CreateChatSessionRequest (JSON body)
        // Response: ChatSessionResponse (201 Created) + Location ヘッダー
        // Error: 400 Validation Problem
        group.MapPost("/sessions", CreateSession)
            .AllowAnonymous()
            .WithName("CreateChatSession")
            .Produces<ChatSessionResponse>(201)
            .ProducesValidationProblem();

        // POST /api/v1/ai/chat/sessions/{sessionId}/messages
        // セッションにメッセージを送信し、AI からの応答を取得する（§2.1 画面 19: 認証不要*）。
        // Request: SendMessageRequest (JSON body), sessionId (path)
        // Response: ChatMessageResponse (200 OK)
        // Error: 400 Validation Problem, 404 Session Not Found
        group.MapPost("/sessions/{sessionId}/messages", SendMessage)
            .AllowAnonymous()
            .WithName("SendMessage")
            .Produces<ChatMessageResponse>(200)
            .ProducesValidationProblem()
            .ProducesProblem(404);

        // GET /api/v1/ai/chat/sessions
        // ログインユーザーの全セッション一覧を取得する。
        // Request: page (int), pageSize (int) クエリパラメータ
        // Response: PagedResult<ChatSessionResponse> (200 OK)
        // Error: 401 Unauthorized
        group.MapGet("/sessions", GetSessions)
            .WithName("GetChatSessions")
            .Produces<List<ChatSessionResponse>>(200)
            .ProducesProblem(401);

        // GET /api/v1/ai/chat/sessions/{sessionId}
        // 特定セッションの詳細情報を取得する。
        // Request: sessionId (path)
        // Response: ChatSessionResponse (200 OK)
        // Error: 401 Unauthorized, 404 Session Not Found
        group.MapGet("/sessions/{sessionId}", GetSessionById)
            .WithName("GetChatSessionById")
            .Produces<ChatSessionResponse>(200)
            .ProducesProblem(401)
            .ProducesProblem(404);

        // GET /api/v1/ai/chat/sessions/{sessionId}/messages
        // セッション内のメッセージ履歴を取得する。
        // Request: sessionId (path), page (int), pageSize (int) クエリパラメータ
        // Response: PagedResult<ChatMessageResponse> (200 OK)
        // Error: 401 Unauthorized, 404 Session Not Found
        group.MapGet("/sessions/{sessionId}/messages", GetMessages)
            .WithName("GetChatMessages")
            .Produces<List<ChatMessageResponse>>(200)
            .ProducesProblem(401)
            .ProducesProblem(404);

        // POST /api/v1/ai/chat/sessions/{sessionId}/close
        // セッションを終了する。
        // Request: sessionId (path)
        // Response: 204 No Content
        // Error: 401 Unauthorized, 404 Session Not Found, 409 Conflict（既に終了済み）
        group.MapPost("/sessions/{sessionId}/close", CloseSession)
            .WithName("CloseChatSession")
            .Produces(204)
            .ProducesProblem(401)
            .ProducesProblem(404)
            .ProducesProblem(409);

        // POST /api/v1/ai/chat/sessions/{sessionId}/escalate
        // 有人サポートにエスカレーションする。
        // Request: sessionId (path)
        // Response: 200 OK（メッセージ付き）
        // Error: 401 Unauthorized, 404 Session Not Found, 409 Conflict（既にエスカレーション済み）
        group.MapPost("/sessions/{sessionId}/escalate", EscalateSession)
            .WithName("EscalateChatSession")
            .Produces(200)
            .ProducesProblem(401)
            .ProducesProblem(404)
            .ProducesProblem(409);
    }

    /// <summary>
    /// 新規チャットセッションを作成する。
    /// </summary>
    /// <param name="request">セッション作成リクエスト。</param>
    /// <param name="validator">リクエストバリデーター。</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal。</param>
    /// <param name="chatService">チャットサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>作成されたセッション情報（201 Created）。</returns>
    /// <exception cref="UnauthorizedException">ユーザー ID が取得できない場合。</exception>
    private static async Task<IResult> CreateSession(
        CreateChatSessionRequest request,
        IValidator<CreateChatSessionRequest> validator,
        ClaimsPrincipal user,
        IChatService chatService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // 未ログイン時はゲスト ID を生成（§2.1 画面 19: 認証不要*）
        // user_profiles.user_id は varchar(36) のため 36 文字以内に収める
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? $"g-{Guid.NewGuid():N}";
        var session = await chatService.CreateSessionAsync(userId, request, ct);
        return Results.Created($"/api/v1/ai/chat/sessions/{session.Id}", session);
    }

    /// <summary>
    /// セッションにメッセージを送信し、AI からの応答を取得する。
    /// </summary>
    /// <param name="sessionId">対象セッション ID。</param>
    /// <param name="request">メッセージ送信リクエスト。</param>
    /// <param name="validator">リクエストバリデーター。</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal。</param>
    /// <param name="chatService">チャットサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>AI からの応答メッセージ。</returns>
    /// <exception cref="UnauthorizedException">ユーザー ID が取得できない場合。</exception>
    private static async Task<IResult> SendMessage(
        string sessionId,
        SendMessageRequest request,
        IValidator<SendMessageRequest> validator,
        ClaimsPrincipal user,
        IChatService chatService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // 未ログイン時はゲスト ID を許容（§2.1 画面 19: 認証不要*）
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "guest-anonymous";
        var response = await chatService.SendMessageAsync(sessionId, userId, request, ct);
        return Results.Ok(response);
    }

    /// <summary>
    /// ログインユーザーの全セッション一覧を取得する。
    /// </summary>
    /// <param name="page">ページ番号（1以上、デフォルト: 1）。</param>
    /// <param name="pageSize">1ページあたりの件数（1〜100、デフォルト: 20）。</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal。</param>
    /// <param name="chatService">チャットサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>ページネーションされたセッション一覧。</returns>
    /// <exception cref="UnauthorizedException">ユーザー ID が取得できない場合。</exception>
    private static async Task<IResult> GetSessions(
        int page,
        int pageSize,
        ClaimsPrincipal user,
        IChatService chatService,
        CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await chatService.GetSessionsAsync(userId, page, pageSize, ct));
    }

    /// <summary>
    /// セッション内のメッセージ履歴を取得する。
    /// </summary>
    /// <param name="sessionId">対象セッション ID。</param>
    /// <param name="page">ページ番号（1以上、デフォルト: 1）。</param>
    /// <param name="pageSize">1ページあたりの件数（1〜100、デフォルト: 50）。</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal。</param>
    /// <param name="chatService">チャットサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>ページネーションされたメッセージ一覧。</returns>
    /// <exception cref="UnauthorizedException">ユーザー ID が取得できない場合。</exception>
    private static async Task<IResult> GetMessages(
        string sessionId,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        IChatService chatService,
        CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 50;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await chatService.GetMessagesAsync(sessionId, userId, page, pageSize, ct));
    }

    /// <summary>
    /// 特定セッションの詳細情報を取得する。
    /// </summary>
    /// <param name="sessionId">対象セッション ID。</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal。</param>
    /// <param name="chatService">チャットサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>セッション詳細、または 404 Not Found。</returns>
    /// <exception cref="UnauthorizedException">ユーザー ID が取得できない場合。</exception>
    private static async Task<IResult> GetSessionById(
        string sessionId,
        ClaimsPrincipal user,
        IChatService chatService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return await chatService.GetSessionByIdAsync(sessionId, userId, ct) is { } session
            ? Results.Ok(session)
            : Results.NotFound();
    }

    /// <summary>
    /// セッションを終了する。
    /// </summary>
    /// <param name="sessionId">終了するセッション ID。</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal。</param>
    /// <param name="chatService">チャットサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>204 No Content。</returns>
    /// <exception cref="UnauthorizedException">ユーザー ID が取得できない場合。</exception>
    private static async Task<IResult> CloseSession(
        string sessionId,
        ClaimsPrincipal user,
        IChatService chatService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        await chatService.CloseSessionAsync(sessionId, userId, ct);
        return Results.NoContent();
    }

    /// <summary>
    /// セッションを有人サポートにエスカレーションする。
    /// </summary>
    /// <param name="sessionId">エスカレーションするセッション ID。</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal。</param>
    /// <param name="chatService">チャットサービス。</param>
    /// <param name="ct">キャンセレーショントークン。</param>
    /// <returns>エスカレーション完了メッセージ（200 OK）。</returns>
    /// <exception cref="UnauthorizedException">ユーザー ID が取得できない場合。</exception>
    private static async Task<IResult> EscalateSession(
        string sessionId,
        ClaimsPrincipal user,
        IChatService chatService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        await chatService.EscalateSessionAsync(sessionId, userId, ct);
        return Results.Ok(new { Message = "有人サポートにエスカレーションしました" });
    }
}
