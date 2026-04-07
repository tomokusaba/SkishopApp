// ─────────────────────────────────────────────────────────────
// CircuitBreakerService — クラスター別サーキットブレーカー状態管理
//
// 設計書 §7 に基づき、各バックエンドクラスターの失敗率を追跡し、
// しきい値超過時にサーキットを Open → Half-Open → Closed と遷移させる。
// ─────────────────────────────────────────────────────────────

using System.Collections.Concurrent;
using ApiGateway.Infrastructure.Metrics;

namespace ApiGateway.Infrastructure.Resilience;

/// <summary>
/// サーキットブレーカーの状態を表す列挙型。
/// </summary>
public enum CircuitState
{
    /// <summary>正常動作。リクエストをバックエンドに転送する。</summary>
    Closed,
    /// <summary>障害検出。リクエストを即座に拒否する。</summary>
    Open,
    /// <summary>回復試行中。限定数のリクエストのみ転送して回復を確認する。</summary>
    HalfOpen
}

/// <summary>
/// クラスター別のサーキットブレーカー設定。設計書 §7 の設定値を保持する。
/// </summary>
/// <param name="FailureRatioThreshold">サーキットを Open にする失敗率しきい値（0.0〜1.0）。</param>
/// <param name="ResetTimeout">Open → Half-Open に遷移するまでの待機時間。</param>
/// <param name="HalfOpenMaxRequests">Half-Open 状態で許可する最大リクエスト数。</param>
/// <param name="SamplingDuration">失敗率を計算するサンプリングウィンドウ。</param>
public record CircuitBreakerSettings(
    double FailureRatioThreshold,
    TimeSpan ResetTimeout,
    int HalfOpenMaxRequests,
    TimeSpan SamplingDuration);

/// <summary>
/// クラスターごとのサーキットブレーカー状態を管理するサービス。
/// <para>
/// YARP のリクエスト転送結果（成功/失敗）を追跡し、設計書 §7 のパラメータに基づいて
/// サーキット状態を Open / Half-Open / Closed に遷移させる。
/// <see cref="TimeProvider"/> を使用してテスタビリティを確保する。
/// </para>
/// </summary>
public sealed class CircuitBreakerService : ICircuitBreakerService
{
    private readonly ConcurrentDictionary<string, ClusterCircuitState> _states = new();
    private readonly ILogger<CircuitBreakerService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly GatewayMetrics _metrics;

    /// <summary>設計書 §7 準拠のクラスター別サーキットブレーカー設定。</summary>
    private static readonly Dictionary<string, CircuitBreakerSettings> ClusterSettings = new()
    {
        ["auth-cluster"] = new(0.50, TimeSpan.FromSeconds(30), 10, TimeSpan.FromSeconds(30)),
        ["user-cluster"] = new(0.50, TimeSpan.FromSeconds(60), 5, TimeSpan.FromSeconds(30)),
        ["inventory-cluster"] = new(0.60, TimeSpan.FromSeconds(60), 10, TimeSpan.FromSeconds(30)),
        ["sales-cluster"] = new(0.50, TimeSpan.FromSeconds(60), 10, TimeSpan.FromSeconds(30)),
        ["payment-cart-cluster"] = new(0.30, TimeSpan.FromSeconds(120), 5, TimeSpan.FromSeconds(30)),
        ["points-cluster"] = new(0.50, TimeSpan.FromSeconds(60), 5, TimeSpan.FromSeconds(30)),
        ["coupons-cluster"] = new(0.60, TimeSpan.FromSeconds(45), 10, TimeSpan.FromSeconds(30)),
        ["ai-cluster"] = new(0.70, TimeSpan.FromSeconds(30), 10, TimeSpan.FromSeconds(30)),
        // R-C2: MailSendService 用サーキットブレーカー設定（管理者専用、高信頼性）
        ["mail-cluster"] = new(0.50, TimeSpan.FromSeconds(60), 5, TimeSpan.FromSeconds(30))
    };

    /// <summary>
    /// <see cref="CircuitBreakerService"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="logger">ログ出力用。</param>
    /// <param name="timeProvider">時刻取得用（テスト時に差し替え可能）。</param>
    /// <param name="metrics">メトリクス記録用。</param>
    public CircuitBreakerService(
        ILogger<CircuitBreakerService> logger,
        TimeProvider timeProvider,
        GatewayMetrics metrics)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _metrics = metrics;

        // 各クラスターの初期状態を Closed で生成
        foreach (var (clusterId, _) in ClusterSettings)
        {
            _states[clusterId] = new ClusterCircuitState(timeProvider);
        }
    }

    /// <summary>
    /// 指定クラスターのサーキットブレーカー設定を取得する。
    /// </summary>
    public static CircuitBreakerSettings? GetSettings(string clusterId)
        => ClusterSettings.GetValueOrDefault(clusterId);

    /// <inheritdoc/>
    public bool AllowRequest(string clusterId)
    {
        if (!_states.TryGetValue(clusterId, out var state))
            return true; // 未知のクラスターは許可

        var settings = ClusterSettings.GetValueOrDefault(clusterId);
        if (settings is null) return true;

        lock (state)
        {
            switch (state.State)
            {
                case CircuitState.Closed:
                    return true;

                case CircuitState.Open:
                    // リセットタイムアウト経過 → Half-Open へ遷移
                    if (_timeProvider.GetUtcNow().UtcDateTime - state.LastStateChange >= settings.ResetTimeout)
                    {
                        state.State = CircuitState.HalfOpen;
                        state.HalfOpenRequestCount = 0;
                        state.LastStateChange = _timeProvider.GetUtcNow().UtcDateTime;
                        _logger.LogInformation(
                            "サーキットブレーカー Half-Open: {ClusterId}", clusterId);
                        _metrics.CircuitBreakerState.Add(1,
                            new KeyValuePair<string, object?>("cluster", clusterId),
                            new KeyValuePair<string, object?>("state", "half_open"));
                        return true;
                    }
                    return false;

                case CircuitState.HalfOpen:
                    // Half-Open 中は限定数のみ許可
                    if (state.HalfOpenRequestCount < settings.HalfOpenMaxRequests)
                    {
                        state.HalfOpenRequestCount++;
                        return true;
                    }
                    return false;

                default:
                    return true;
            }
        }
    }

    /// <inheritdoc/>
    public void RecordSuccess(string clusterId)
    {
        if (!_states.TryGetValue(clusterId, out var state)) return;
        var settings = ClusterSettings.GetValueOrDefault(clusterId);
        if (settings is null) return;

        _metrics.CircuitBreakerCalls.Add(1,
            new KeyValuePair<string, object?>("cluster", clusterId),
            new KeyValuePair<string, object?>("result", "success"));

        lock (state)
        {
            state.SuccessCount++;
            PurgeOldEntries(state, settings.SamplingDuration);

            if (state.State == CircuitState.HalfOpen)
            {
                // Half-Open で成功 → Closed に回復
                state.State = CircuitState.Closed;
                state.LastStateChange = _timeProvider.GetUtcNow().UtcDateTime;
                state.ResetCounters();
                _logger.LogInformation(
                    "サーキットブレーカー Closed（回復）: {ClusterId}", clusterId);
                _metrics.CircuitBreakerState.Add(0,
                    new KeyValuePair<string, object?>("cluster", clusterId),
                    new KeyValuePair<string, object?>("state", "closed"));
            }
        }
    }

    /// <inheritdoc/>
    public void RecordFailure(string clusterId)
    {
        if (!_states.TryGetValue(clusterId, out var state)) return;
        var settings = ClusterSettings.GetValueOrDefault(clusterId);
        if (settings is null) return;

        _metrics.CircuitBreakerCalls.Add(1,
            new KeyValuePair<string, object?>("cluster", clusterId),
            new KeyValuePair<string, object?>("result", "failure"));

        lock (state)
        {
            state.FailureCount++;
            PurgeOldEntries(state, settings.SamplingDuration);

            if (state.State == CircuitState.HalfOpen)
            {
                // Half-Open で失敗 → 再度 Open
                state.State = CircuitState.Open;
                state.LastStateChange = _timeProvider.GetUtcNow().UtcDateTime;
                _logger.LogWarning(
                    "サーキットブレーカー再 Open（Half-Open 中に失敗）: {ClusterId}", clusterId);
                _metrics.CircuitBreakerState.Add(2,
                    new KeyValuePair<string, object?>("cluster", clusterId),
                    new KeyValuePair<string, object?>("state", "open"));
                return;
            }

            // Closed 状態で失敗率チェック
            var totalRequests = state.SuccessCount + state.FailureCount;
            if (totalRequests >= 10) // 最小スループット
            {
                var failureRatio = (double)state.FailureCount / totalRequests;
                if (failureRatio >= settings.FailureRatioThreshold)
                {
                    state.State = CircuitState.Open;
                    state.LastStateChange = _timeProvider.GetUtcNow().UtcDateTime;
                    _logger.LogWarning(
                        "サーキットブレーカー Open: {ClusterId}, 失敗率={FailureRatio:P1}",
                        clusterId, failureRatio);
                    _metrics.CircuitBreakerState.Add(2,
                        new KeyValuePair<string, object?>("cluster", clusterId),
                        new KeyValuePair<string, object?>("state", "open"));
                }
            }
        }
    }

    /// <inheritdoc/>
    public CircuitState GetState(string clusterId)
        => _states.TryGetValue(clusterId, out var state) ? state.State : CircuitState.Closed;

    /// <summary>サンプリングウィンドウ外の古いエントリをリセットする。</summary>
    private void PurgeOldEntries(ClusterCircuitState state, TimeSpan samplingDuration)
    {
        if (_timeProvider.GetUtcNow().UtcDateTime - state.WindowStart > samplingDuration)
        {
            state.ResetCounters();
        }
    }

    /// <summary>クラスターごとの内部状態を保持するクラス。</summary>
    private sealed class ClusterCircuitState
    {
        private readonly TimeProvider _tp;

        public ClusterCircuitState(TimeProvider timeProvider)
        {
            _tp = timeProvider;
            LastStateChange = _tp.GetUtcNow().UtcDateTime;
            WindowStart = _tp.GetUtcNow().UtcDateTime;
        }

        public CircuitState State { get; set; } = CircuitState.Closed;
        public DateTime LastStateChange { get; set; }
        public DateTime WindowStart { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int HalfOpenRequestCount { get; set; }

        public void ResetCounters()
        {
            SuccessCount = 0;
            FailureCount = 0;
            WindowStart = _tp.GetUtcNow().UtcDateTime;
        }
    }
}
