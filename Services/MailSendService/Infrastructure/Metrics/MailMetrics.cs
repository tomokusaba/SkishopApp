using System.Diagnostics.Metrics;

namespace MailSendService.Infrastructure.Metrics;

/// <summary>
/// メール送信に関する OpenTelemetry メトリクスを記録するクラス。
/// </summary>
/// <remarks>
/// <para>送信成功/失敗/スキップ/リトライ/イベント受信のカウンターと、
/// 送信処理時間のヒストグラムを提供する。</para>
/// <para>メソッドは <c>virtual</c> 修飾されており、テスト時に NSubstitute でモック可能。</para>
/// </remarks>
public class MailMetrics
{
    private readonly Counter<long> _mailSentCounter;
    private readonly Counter<long> _mailFailedCounter;
    private readonly Counter<long> _mailSkippedCounter;
    private readonly Counter<long> _mailRetryCounter;
    private readonly Counter<long> _mailEventConsumedCounter;
    private readonly Histogram<double> _sendDurationHistogram;

    /// <summary>
    /// <see cref="MailMetrics"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="meterFactory">OpenTelemetry メーターファクトリ。</param>
    public MailMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("MailSendService.Metrics");
        _mailSentCounter = meter.CreateCounter<long>("mail.sent.count", "mails", "メール送信成功数");
        _mailFailedCounter = meter.CreateCounter<long>("mail.failed.count", "mails", "メール送信失敗数");
        _mailSkippedCounter = meter.CreateCounter<long>("mail.skipped.count", "mails", "メール送信スキップ数");
        _mailRetryCounter = meter.CreateCounter<long>("mail.retry.count", "retries", "リトライ実行回数");
        _mailEventConsumedCounter = meter.CreateCounter<long>("mail.event.consumed.count", "events", "受信イベント総数");
        _sendDurationHistogram = meter.CreateHistogram<double>("mail.send.duration", "ms", "メール送信処理時間");
    }

    /// <summary>
    /// メール送信成功を記録する。
    /// </summary>
    /// <param name="eventType">イベントタイプ（例: "order.created"）。</param>
    public virtual void RecordMailSent(string eventType)
        => _mailSentCounter.Add(1, new KeyValuePair<string, object?>("event_type", eventType));

    /// <summary>
    /// メール送信失敗を記録する。
    /// </summary>
    /// <param name="eventType">イベントタイプ。</param>
    /// <param name="reason">失敗理由。</param>
    public virtual void RecordMailFailed(string eventType, string reason)
        => _mailFailedCounter.Add(1,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("reason", reason));

    /// <summary>
    /// メール送信スキップを記録する（抑制リスト該当等）。
    /// </summary>
    /// <param name="eventType">イベントタイプ。</param>
    /// <param name="reason">スキップ理由。</param>
    public virtual void RecordMailSkipped(string eventType, string reason)
        => _mailSkippedCounter.Add(1,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("reason", reason));

    /// <summary>
    /// メール再送リトライの実行を記録する。
    /// </summary>
    /// <param name="eventType">イベントタイプ。</param>
    public virtual void RecordMailRetry(string eventType)
        => _mailRetryCounter.Add(1,
            new KeyValuePair<string, object?>("event_type", eventType));

    /// <summary>
    /// Kafka イベント受信を記録する。
    /// </summary>
    /// <param name="eventType">受信したイベントタイプ。</param>
    public virtual void RecordEventConsumed(string eventType)
        => _mailEventConsumedCounter.Add(1,
            new KeyValuePair<string, object?>("event_type", eventType));

    /// <summary>
    /// メール送信処理の所要時間をヒストグラムに記録する。
    /// </summary>
    /// <param name="milliseconds">処理時間（ミリ秒）。</param>
    /// <param name="eventType">イベントタイプ。</param>
    public virtual void RecordSendDuration(double milliseconds, string eventType)
        => _sendDurationHistogram.Record(milliseconds,
            new KeyValuePair<string, object?>("event_type", eventType));
}
