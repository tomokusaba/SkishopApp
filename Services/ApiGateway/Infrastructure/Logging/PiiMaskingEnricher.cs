// ─────────────────────────────────────────────────────────────
// PiiMaskingEnricher — Serilog PII マスキングエンリッチャー
//
// ログイベントのプロパティ値に含まれる個人情報（PII）を自動検出し、
// マスキングした値に置換する。設計書 §9 PII マスキング準拠。
// ─────────────────────────────────────────────────────────────

using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace ApiGateway.Infrastructure.Logging;

/// <summary>
/// Serilog ログイベントのプロパティ値に含まれる PII（個人識別情報）を
/// 自動検出してマスキングするエンリッチャー。
/// <list type="bullet">
///   <item><description>Authorization ヘッダー → <c>Bearer ***...***</c></description></item>
///   <item><description>メールアドレス → <c>u***@example.com</c></description></item>
///   <item><description>クレジットカード番号 → <c>****-****-****-1234</c></description></item>
/// </list>
/// </summary>
public sealed partial class PiiMaskingEnricher : ILogEventEnricher
{
    /// <summary>Bearer トークンの正規表現パターン。</summary>
    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*")]
    private static partial Regex BearerTokenPattern();

    /// <summary>メールアドレスの正規表現パターン。</summary>
    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}")]
    private static partial Regex EmailPattern();

    /// <summary>クレジットカード番号の正規表現パターン（16 桁、ハイフン/スペース区切り対応）。</summary>
    [GeneratedRegex(@"\b(\d{4})[\s\-]?(\d{4})[\s\-]?(\d{4})[\s\-]?(\d{4})\b")]
    private static partial Regex CreditCardPattern();

    /// <summary>
    /// ログイベントの全プロパティを走査し、PII を検出した場合はマスキング済み値に置換する。
    /// </summary>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var propertiesToUpdate = new List<LogEventProperty>();

        foreach (var property in logEvent.Properties)
        {
            if (property.Value is ScalarValue { Value: string stringValue })
            {
                var masked = MaskPii(stringValue);
                if (masked != stringValue)
                {
                    propertiesToUpdate.Add(
                        propertyFactory.CreateProperty(property.Key, masked));
                }
            }
        }

        foreach (var prop in propertiesToUpdate)
        {
            logEvent.AddOrUpdateProperty(prop);
        }
    }

    /// <summary>
    /// 文字列中の PII パターンをマスキングした値に置換する。
    /// </summary>
    /// <param name="value">検査対象の文字列。</param>
    /// <returns>PII がマスキングされた文字列。PII が含まれない場合は元の値。</returns>
    public static string MaskPii(string value)
    {
        // Bearer トークン → "Bearer ***...***"
        var result = BearerTokenPattern().Replace(value, "Bearer ***...***");

        // メールアドレス → "u***@domain"
        result = EmailPattern().Replace(result, match =>
        {
            var parts = match.Value.Split('@');
            if (parts.Length != 2) return "***@***";
            var local = parts[0];
            var domain = parts[1];
            var maskedLocal = local.Length > 1
                ? $"{local[0]}***"
                : "***";
            return $"{maskedLocal}@{domain}";
        });

        // クレジットカード番号 → "****-****-****-1234"
        result = CreditCardPattern().Replace(result, "****-****-****-$4");

        return result;
    }
}
