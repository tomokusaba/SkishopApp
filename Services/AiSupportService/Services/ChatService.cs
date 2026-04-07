using System.Diagnostics;
using System.Text.Json;
using AiSupportService.Configurations;
using AiSupportService.DTOs.Requests;
using AiSupportService.DTOs.Responses;
using AiSupportService.Exceptions;
using AiSupportService.Infrastructure.Metrics;
using AiSupportService.Infrastructure.SemanticKernel;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using AiSupportService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiSupportService.Services;

/// <summary>
/// <see cref="IChatService"/> の実装。Semantic Kernel を使用して AI チャット応答を生成する。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、ユーザーとの会話セッションを管理し、
/// Microsoft Semantic Kernel を使用して AI による自動応答を生成します。
/// </para>
/// <para>
/// セキュリティ機能:
/// <list type="bullet">
///   <item><description>入力サニタイズ: <see cref="InputSanitizer"/> によるプロンプトインジェクション検出</description></item>
///   <item><description>出力フィルタリング: <see cref="ResponseFilter"/> による不適切な応答の除去</description></item>
///   <item><description>セッション分離: ユーザー ID によるアクセス制御</description></item>
/// </list>
/// </para>
/// <para>
/// 可観測性:
/// <list type="bullet">
///   <item><description>セッション作成、メッセージ送信のメトリクス記録</description></item>
///   <item><description>AI 応答生成時間の計測</description></item>
///   <item><description>プロンプトインジェクション検出のメトリクス</description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="sessionRepository">チャットセッションリポジトリ。</param>
/// <param name="messageRepository">チャットメッセージリポジトリ。</param>
/// <param name="userProfileRepository">ユーザープロファイルリポジトリ。</param>
/// <param name="outboxEventRepository">Outbox イベントリポジトリ。</param>
/// <param name="kernel">Semantic Kernel インスタンス。</param>
/// <param name="metrics">AI サポートメトリクス。</param>
/// <param name="chatSettings">チャット設定。</param>
/// <param name="logger">ロガー。</param>
public class ChatService(
    IChatSessionRepository sessionRepository,
    IChatMessageRepository messageRepository,
    IUserProfileRepository userProfileRepository,
    IOutboxEventRepository outboxEventRepository,
    Kernel kernel,
    AiSupportMetrics metrics,
    IOptions<AiChatSettings> chatSettings,
    ILogger<ChatService> logger) : IChatService
{
    /// <summary>
    /// AI チャット設定（セッション上限、メッセージ上限、トークン制限など）。
    /// </summary>
    private readonly AiChatSettings _settings = chatSettings.Value;

    /// <inheritdoc />
    /// <exception cref="BusinessException">
    /// ユーザーのアクティブセッション数が <see cref="AiChatSettings.MaxSessionsPerUser"/> に達している場合にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// セッション作成の処理フロー:
    /// <list type="number">
    ///   <item><description>ユーザーのアクティブセッション数をチェック</description></item>
    ///   <item><description>ユーザープロファイルが存在しない場合は自動作成</description></item>
    ///   <item><description>新しいセッションを作成し、システムプロンプトを追加</description></item>
    ///   <item><description>セッション作成メトリクスを記録</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task<ChatSessionResponse> CreateSessionAsync(
        string userId, CreateChatSessionRequest request, CancellationToken ct = default)
    {
        var activeCount = await sessionRepository.CountActiveByUserIdAsync(userId, ct);
        if (activeCount >= _settings.MaxSessionsPerUser)
            throw new BusinessException($"アクティブセッション数の上限（{_settings.MaxSessionsPerUser}）に達しています");

        await userProfileRepository.GetOrCreateAsync(userId, ct);

        var session = new ChatSession
        {
            UserId = userId,
            Title = request.Title ?? "新しいチャット",
            Status = "ACTIVE"
        };

        session.AddMessage("system", SystemPromptProvider.GetSystemPrompt());

        await sessionRepository.AddAsync(session, ct);
        await sessionRepository.SaveChangesAsync(ct);

        metrics.RecordChatSession();
        logger.LogInformation("チャットセッション作成: SessionId={SessionId}, UserId={UserId}", session.Id, userId);

        return ToSessionResponse(session);
    }

    /// <inheritdoc />
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
    /// メッセージ処理の詳細フロー:
    /// <list type="number">
    ///   <item><description>セッションの存在確認とアクセス権チェック</description></item>
    ///   <item><description>セッションがアクティブかどうかの確認</description></item>
    ///   <item><description>メッセージ数上限のチェック</description></item>
    ///   <item><description><see cref="InputSanitizer"/> による入力サニタイズとプロンプトインジェクション検出</description></item>
    ///   <item><description>過去の会話履歴から <see cref="ChatHistory"/> を構築</description></item>
    ///   <item><description><see cref="IChatCompletionService"/> を使用して AI 応答を生成</description></item>
    ///   <item><description><see cref="ResponseFilter"/> による応答フィルタリング</description></item>
    ///   <item><description>メッセージの保存とメトリクス記録</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// AI 応答生成に使用される設定:
    /// <list type="bullet">
    ///   <item><description>max_completion_tokens: <see cref="AiChatSettings.MaxOutputTokens"/></description></item>
    ///   <item><description>temperature: モデルのデフォルト値を使用（o1/gpt-5 系モデルでは 1.0 固定）</description></item>
    ///   <item><description>function_calling: Auto（Semantic Kernel プラグイン自動選択）</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task<ChatMessageResponse> SendMessageAsync(
        string sessionId, string userId, SendMessageRequest request, CancellationToken ct = default)
    {
        // ゲストユーザーの場合、セッション ID のみで検索し、ゲストセッションであることを検証
        ChatSession session;
        if (userId == "guest-anonymous")
        {
            session = await sessionRepository.FindByIdAsync(sessionId, ct)
                ?? throw new NotFoundException($"セッション {sessionId} が見つかりません");
            if (!session.UserId.StartsWith("g-"))
                throw new Exceptions.UnauthorizedException();
        }
        else
        {
            session = await sessionRepository.FindByIdAndUserIdAsync(sessionId, userId, ct)
                ?? throw new NotFoundException($"セッション {sessionId} が見つかりません");
        }

        if (!session.IsActive)
            throw new BusinessException("このセッションはアクティブではありません");

        var messageCount = await messageRepository.CountBySessionIdAsync(sessionId, ct);
        if (messageCount >= _settings.MaxMessagesPerSession)
            throw new BusinessException($"メッセージ数の上限（{_settings.MaxMessagesPerSession}）に達しています");

        var (sanitized, isBlocked, blockReason) = InputSanitizer.Sanitize(request.Message);
        if (isBlocked)
        {
            metrics.RecordPromptInjectionBlocked();
            logger.LogWarning("プロンプトインジェクション検出: SessionId={SessionId}", sessionId);
            throw new Exceptions.SecurityException(blockReason ?? "不正な入力が検出されました");
        }

        var chatHistory = new ChatHistory();
        foreach (var msg in session.Messages)
        {
            switch (msg.Role)
            {
                case "system":
                    chatHistory.AddSystemMessage(msg.Content);
                    break;
                case "user":
                    chatHistory.AddUserMessage(msg.Content);
                    break;
                case "assistant":
                    chatHistory.AddAssistantMessage(msg.Content);
                    break;
            }
        }
        chatHistory.AddUserMessage(sanitized);

        session.AddMessage("user", sanitized);

        var stopwatch = Stopwatch.StartNew();
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        // o1 系モデルは temperature=1.0 のみサポート。ExtensionData に設定しない場合はデフォルト値が使用される
        var extensionData = new Dictionary<string, object>
        {
            ["max_completion_tokens"] = _settings.MaxOutputTokens
        };

        // temperature が 1.0 以外の場合のみ設定（o1 系モデルでは無視される可能性があるため）
        // 注意: 一部のモデル（o1, gpt-5 等）では temperature パラメータがサポートされない
        // そのため、temperature の設定は行わずデフォルト値を使用する

        var result = await chatCompletionService.GetChatMessageContentAsync(
            chatHistory,
            new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                ExtensionData = extensionData
            },
            kernel,
            ct);

        var responseContent = ResponseFilter.Filter(result.Content ?? "申し訳ございません。回答を生成できませんでした。");
        stopwatch.Stop();
        metrics.RecordAiResponseDuration(stopwatch.Elapsed.TotalSeconds);
        metrics.RecordChatMessage();

        var assistantMessage = session.AddMessage("assistant", responseContent);
        assistantMessage.MetadataJson = JsonSerializer.Serialize(new { model = result.ModelId });
        await sessionRepository.SaveChangesAsync(ct);

        logger.LogInformation("チャットメッセージ処理完了: SessionId={SessionId}", sessionId);

        return ToMessageResponse(assistantMessage);
    }

    /// <inheritdoc />
    /// <remarks>
    /// セッションは作成日時の降順（新しい順）でソートされます。
    /// </remarks>
    public async Task<PagedResult<ChatSessionResponse>> GetSessionsAsync(
        string userId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (sessions, totalCount) = await sessionRepository.FindByUserIdPagedAsync(userId, page, pageSize, ct);
        var items = sessions.Select(ToSessionResponse).ToList();
        return new PagedResult<ChatSessionResponse>(
            items, totalCount, page, pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize));
    }

    /// <inheritdoc />
    public async Task<ChatSessionResponse?> GetSessionByIdAsync(
        string sessionId, string userId, CancellationToken ct = default)
    {
        var session = await sessionRepository.FindByIdAndUserIdAsync(sessionId, userId, ct);
        return session is not null ? ToSessionResponse(session) : null;
    }

    /// <inheritdoc />
    /// <exception cref="NotFoundException">
    /// 指定されたセッションが存在しない、またはユーザーがアクセス権を持たない場合にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// メッセージは作成日時の昇順（古い順）でソートされます。
    /// </para>
    /// <para>
    /// システムメッセージ（role = "system"）は結果から除外されます。
    /// これは、システムプロンプトがユーザーに表示されることを防ぐためです。
    /// </para>
    /// </remarks>
    public async Task<PagedResult<ChatMessageResponse>> GetMessagesAsync(
        string sessionId, string userId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        _ = await sessionRepository.FindByIdAndUserIdAsync(sessionId, userId, ct)
            ?? throw new NotFoundException($"セッション {sessionId} が見つかりません");

        var messages = await messageRepository.FindBySessionIdAsync(sessionId, ct);
        var filtered = messages.Where(m => m.Role != "system").ToList();
        var totalCount = filtered.Count;
        var paged = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToMessageResponse)
            .ToList();
        return new PagedResult<ChatMessageResponse>(
            paged, totalCount, page, pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize));
    }

    /// <inheritdoc />
    /// <exception cref="NotFoundException">
    /// 指定されたセッションが存在しない、またはユーザーがアクセス権を持たない場合にスローされます。
    /// </exception>
    /// <exception cref="ConcurrencyException">
    /// 楽観的ロックの競合が発生した場合にスローされます。
    /// </exception>
    public async Task CloseSessionAsync(string sessionId, string userId, CancellationToken ct = default)
    {
        var session = await sessionRepository.FindByIdAndUserIdAsync(sessionId, userId, ct)
            ?? throw new NotFoundException($"セッション {sessionId} が見つかりません");

        session.Close();

        try
        {
            await sessionRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: SessionId={SessionId}", sessionId);
            throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。", ex);
        }

        logger.LogInformation("チャットセッションクローズ: SessionId={SessionId}", sessionId);
    }

    /// <inheritdoc />
    /// <exception cref="NotFoundException">
    /// 指定されたセッションが存在しない、またはユーザーがアクセス権を持たない場合にスローされます。
    /// </exception>
    /// <exception cref="ConcurrencyException">
    /// 楽観的ロックの競合が発生した場合にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// エスカレーション処理:
    /// <list type="number">
    ///   <item><description>セッションのステータスを ESCALATED に変更</description></item>
    ///   <item><description>Outbox イベントを作成（<c>ai.chat.escalated</c> トピック）</description></item>
    ///   <item><description>変更をデータベースに保存（トランザクション整合性）</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Outbox パターンにより、セッション更新とイベント発行の原子性が保証されます。
    /// </para>
    /// </remarks>
    public async Task EscalateSessionAsync(string sessionId, string userId, CancellationToken ct = default)
    {
        var session = await sessionRepository.FindByIdAndUserIdAsync(sessionId, userId, ct)
            ?? throw new NotFoundException($"セッション {sessionId} が見つかりません");

        session.Escalate();

        var outboxEvent = new OutboxEvent
        {
            AggregateType = "ChatSession",
            AggregateId = session.Id,
            EventType = "ChatSessionEscalated",
            Topic = "ai.chat.escalated",
            Payload = JsonSerializer.Serialize(new
            {
                SessionId = session.Id,
                UserId = userId,
                Summary = session.Title ?? "チャットセッション",
                OccurredAt = DateTime.UtcNow
            })
        };
        await outboxEventRepository.AddAsync(outboxEvent, ct);
        try
        {
            await sessionRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: SessionId={SessionId}", sessionId);
            throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。", ex);
        }

        logger.LogInformation("チャットセッションエスカレーション: SessionId={SessionId}", sessionId);
    }

    /// <summary>
    /// <see cref="ChatSession"/> エンティティをレスポンス DTO に変換する。
    /// </summary>
    /// <param name="session">変換元のチャットセッションエンティティ。</param>
    /// <returns>チャットセッションレスポンス DTO。</returns>
    private static ChatSessionResponse ToSessionResponse(ChatSession session)
        => new(session.Id, session.UserId, session.Title, session.Status,
               session.CreatedAt, session.UpdatedAt, session.ClosedAt);

    /// <summary>
    /// <see cref="ChatMessage"/> エンティティをレスポンス DTO に変換する。
    /// </summary>
    /// <param name="message">変換元のチャットメッセージエンティティ。</param>
    /// <returns>チャットメッセージレスポンス DTO。</returns>
    private static ChatMessageResponse ToMessageResponse(ChatMessage message)
        => new(message.Id, message.SessionId, message.Role, message.Content,
               message.TokenCount, message.CreatedAt);
}
