using System.Diagnostics.Metrics;

namespace AiSupportService.Infrastructure.Metrics;

/// <summary>
/// AiSupportService のカスタムメトリクスを管理するクラス。
/// </summary>
/// <remarks>
/// <para>
/// OpenTelemetry Meter を使用して、AI サポートサービスの各種メトリクスを計測・公開する。
/// これらのメトリクスは Prometheus や Grafana 等の監視ツールで可視化され、
/// サービスの健全性・パフォーマンス・利用状況の把握に活用される。
/// </para>
/// <para>
/// <b>提供メトリクス:</b>
/// <list type="bullet">
///   <item><description><b>ai.chat.messages.total</b>: チャットメッセージ総数（Counter）</description></item>
///   <item><description><b>ai.chat.sessions.total</b>: チャットセッション総数（Counter）</description></item>
///   <item><description><b>ai.search.requests.total</b>: 検索リクエスト総数（Counter）</description></item>
///   <item><description><b>ai.recommendations.total</b>: レコメンデーション生成総数（Counter、type ラベル付き）</description></item>
///   <item><description><b>ai.security.prompt_injection_blocked.total</b>: プロンプトインジェクションブロック数（Counter）</description></item>
///   <item><description><b>ai.response.duration.seconds</b>: AI レスポンス生成時間（Histogram）</description></item>
///   <item><description><b>ai.search.duration.seconds</b>: 検索レスポンス時間（Histogram）</description></item>
///   <item><description><b>ai.tokens.consumed.total</b>: 消費トークン総数（Counter）</description></item>
///   <item><description><b>ai.service.errors.total</b>: AI サービスエラー総数（Counter、service ラベル付き）</description></item>
///   <item><description><b>ai.forecasts.generated.total</b>: 需要予測生成総数（Counter）</description></item>
/// </list>
/// </para>
/// <para>
/// <b>命名規則:</b> OpenTelemetry Semantic Conventions に準拠し、
/// <c>ai.</c> プレフィックスで AI 関連メトリクスを識別可能にする。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs での登録
/// builder.Services.AddSingleton&lt;AiSupportMetrics&gt;();
/// builder.Services.AddOpenTelemetry()
///     .WithMetrics(metrics => metrics.AddMeter("SkiShop.AiSupportService"));
///
/// // Service での使用
/// public class ChatService(AiSupportMetrics metrics)
/// {
///     public async Task SendMessageAsync(...)
///     {
///         metrics.RecordChatMessage();
///         var sw = Stopwatch.StartNew();
///         // AI 処理...
///         metrics.RecordAiResponseDuration(sw.Elapsed.TotalSeconds);
///     }
/// }
/// </code>
/// </example>
public class AiSupportMetrics
{
    private readonly Counter<long> _chatMessagesTotal;
    private readonly Counter<long> _chatSessionsTotal;
    private readonly Counter<long> _searchRequestsTotal;
    private readonly Counter<long> _recommendationsTotal;
    private readonly Counter<long> _promptInjectionBlockedTotal;
    private readonly Histogram<double> _aiResponseDuration;
    private readonly Histogram<double> _searchResponseDuration;
    private readonly Counter<long> _tokensConsumedTotal;
    private readonly Counter<long> _aiServiceErrorsTotal;
    private readonly Counter<long> _forecastsGeneratedTotal;

    /// <summary>
    /// メトリクス計測器を初期化し、各種カウンター・ヒストグラムを作成する。
    /// </summary>
    /// <param name="meterFactory">
    /// OpenTelemetry Meter ファクトリ。<c>SkiShop.AiSupportService</c> 名の Meter を作成する。
    /// </param>
    public AiSupportMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("SkiShop.AiSupportService");

        _chatMessagesTotal = meter.CreateCounter<long>(
            "ai.chat.messages.total", description: "チャットメッセージ総数");
        _chatSessionsTotal = meter.CreateCounter<long>(
            "ai.chat.sessions.total", description: "チャットセッション総数");
        _searchRequestsTotal = meter.CreateCounter<long>(
            "ai.search.requests.total", description: "検索リクエスト総数");
        _recommendationsTotal = meter.CreateCounter<long>(
            "ai.recommendations.total", description: "レコメンデーション生成総数");
        _promptInjectionBlockedTotal = meter.CreateCounter<long>(
            "ai.security.prompt_injection_blocked.total", description: "プロンプトインジェクションブロック数");
        _aiResponseDuration = meter.CreateHistogram<double>(
            "ai.response.duration.seconds", description: "AI レスポンス生成時間");
        _searchResponseDuration = meter.CreateHistogram<double>(
            "ai.search.duration.seconds", description: "検索レスポンス時間");
        _tokensConsumedTotal = meter.CreateCounter<long>(
            "ai.tokens.consumed.total", description: "消費トークン総数");
        _aiServiceErrorsTotal = meter.CreateCounter<long>(
            "ai.service.errors.total", description: "AI サービスエラー総数");
        _forecastsGeneratedTotal = meter.CreateCounter<long>(
            "ai.forecasts.generated.total", description: "需要予測生成総数");
    }

    /// <summary>
    /// チャットメッセージ数を 1 加算する。
    /// </summary>
    /// <remarks>
    /// ユーザーメッセージ・AI レスポンスの両方でカウントすることを推奨。
    /// </remarks>
    public void RecordChatMessage() => _chatMessagesTotal.Add(1);

    /// <summary>
    /// チャットセッション数を 1 加算する。
    /// </summary>
    /// <remarks>
    /// 新規セッション作成時にのみカウントする。
    /// </remarks>
    public void RecordChatSession() => _chatSessionsTotal.Add(1);

    /// <summary>
    /// 検索リクエスト数を 1 加算する。
    /// </summary>
    public void RecordSearchRequest() => _searchRequestsTotal.Add(1);

    /// <summary>
    /// レコメンデーション生成数を種別ごとに 1 加算する。
    /// </summary>
    /// <param name="type">
    /// レコメンデーション種別。以下の値が想定される:
    /// <list type="bullet">
    ///   <item><description>PERSONALIZED: パーソナライズド</description></item>
    ///   <item><description>TRENDING: トレンド</description></item>
    ///   <item><description>SIMILAR: 類似商品</description></item>
    ///   <item><description>SEASONAL: 季節商品</description></item>
    ///   <item><description>FREQUENTLY_BOUGHT_TOGETHER: 一緒に購入される商品</description></item>
    /// </list>
    /// </param>
    public void RecordRecommendation(string type)
        => _recommendationsTotal.Add(1, new KeyValuePair<string, object?>("type", type));

    /// <summary>
    /// プロンプトインジェクションブロック数を 1 加算する。
    /// </summary>
    /// <remarks>
    /// <see cref="InputSanitizer"/> が危険なパターンを検出してブロックした際に呼び出す。
    /// このメトリクスの急増は攻撃の兆候を示す可能性がある。
    /// </remarks>
    public void RecordPromptInjectionBlocked() => _promptInjectionBlockedTotal.Add(1);

    /// <summary>
    /// AI レスポンス生成時間を記録する。
    /// </summary>
    /// <param name="seconds">レスポンス生成に要した秒数。</param>
    /// <remarks>
    /// Azure OpenAI への API 呼び出しを含む全体の応答時間を計測する。
    /// P50/P95/P99 パーセンタイルでの分析を推奨。
    /// </remarks>
    public void RecordAiResponseDuration(double seconds) => _aiResponseDuration.Record(seconds);

    /// <summary>
    /// 検索レスポンス時間を記録する。
    /// </summary>
    /// <param name="seconds">検索に要した秒数。</param>
    /// <remarks>
    /// Azure AI Search への API 呼び出しを含む全体の応答時間を計測する。
    /// </remarks>
    public void RecordSearchDuration(double seconds) => _searchResponseDuration.Record(seconds);

    /// <summary>
    /// 消費トークン数を加算する。
    /// </summary>
    /// <param name="tokens">消費したトークン数（入力トークン + 出力トークン）。</param>
    /// <remarks>
    /// Azure OpenAI のコスト管理に活用する。
    /// Usage.TotalTokens から取得した値を渡すことを推奨。
    /// </remarks>
    public void RecordTokensConsumed(int tokens) => _tokensConsumedTotal.Add(tokens);

    /// <summary>
    /// AI サービスエラー数をサービス別に 1 加算する。
    /// </summary>
    /// <param name="service">
    /// エラーが発生したサービス名。以下の値が想定される:
    /// <list type="bullet">
    ///   <item><description>AzureOpenAI: Azure OpenAI Service</description></item>
    ///   <item><description>AzureAISearch: Azure AI Search</description></item>
    ///   <item><description>Kafka: Kafka Producer/Consumer</description></item>
    /// </list>
    /// </param>
    public void RecordAiServiceError(string service)
        => _aiServiceErrorsTotal.Add(1, new KeyValuePair<string, object?>("service", service));

    /// <summary>
    /// 需要予測生成数を 1 加算する。
    /// </summary>
    public void RecordForecastGenerated() => _forecastsGeneratedTotal.Add(1);
}
