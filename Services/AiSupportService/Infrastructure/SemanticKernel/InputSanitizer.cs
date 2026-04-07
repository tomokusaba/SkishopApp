using System.Text.RegularExpressions;

namespace AiSupportService.Infrastructure.SemanticKernel;

/// <summary>
/// ユーザー入力のサニタイズとプロンプトインジェクション防止を担当する静的ユーティリティ。
/// </summary>
/// <remarks>
/// <para>
/// <b>セキュリティ目的:</b>
/// OWASP LLM Top 10 の LLM01（プロンプトインジェクション）対策として、
/// AI に送信する前にユーザー入力を検証・サニタイズする。
/// </para>
/// <para>
/// <b>検出対象パターン:</b>
/// <list type="bullet">
///   <item><description><b>ロール偽装:</b> "you are", "act as", "pretend to be" 等の指示</description></item>
///   <item><description><b>指示無視:</b> "ignore previous instructions", "forget everything" 等</description></item>
///   <item><description><b>プロンプト漏洩:</b> "reveal system prompt", "show instructions" 等</description></item>
///   <item><description><b>安全機能バイパス:</b> "bypass safety", "disable filter" 等</description></item>
///   <item><description><b>XSS パターン:</b> <c>&lt;script&gt;</c>, <c>javascript:</c> 等</description></item>
///   <item><description><b>SQL インジェクション:</b> "DROP TABLE", "DELETE FROM" 等</description></item>
///   <item><description><b>Jailbreak 試行:</b> "DAN mode", "jailbreak" 等</description></item>
///   <item><description><b>特殊トークン:</b> <c>&lt;|im_start|&gt;</c> 等の LLM 制御トークン</description></item>
/// </list>
/// </para>
/// <para>
/// <b>サニタイズ処理:</b>
/// <list type="bullet">
///   <item><description>HTML タグの除去（XSS 防止）</description></item>
///   <item><description>LLM 特殊トークンの除去</description></item>
///   <item><description>前後の空白のトリム</description></item>
/// </list>
/// </para>
/// <para>
/// <b>ReDoS 対策:</b> 全ての正規表現に 1 秒のタイムアウトを設定し、
/// 正規表現 DoS 攻撃を防止する。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var (sanitized, isBlocked, reason) = InputSanitizer.Sanitize(userInput);
/// if (isBlocked)
/// {
///     _metrics.RecordPromptInjectionBlocked();
///     throw new BusinessException(reason);
/// }
/// // sanitized を AI に送信
/// </code>
/// </example>
public static partial class InputSanitizer
{
    /// <summary>
    /// プロンプトインジェクションを検出するための危険パターン一覧。
    /// </summary>
    /// <remarks>
    /// 各パターンは大文字小文字を区別せず、1 秒のタイムアウトが設定されている。
    /// 新しい攻撃パターンが発見された場合は、このリストに追加する。
    /// </remarks>
    private static readonly Regex[] DangerousPatterns =
    [
        // 指示無視パターン
        new(@"ignore\s+(previous|above|all)\s+(instructions?|prompts?|rules?)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // ロール偽装パターン
        new(@"(you\s+are|act\s+as|pretend\s+to\s+be|roleplay)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // システムプロンプト参照パターン
        new(@"(system\s*prompt|initial\s*instruction|base\s*prompt)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // 安全機能バイパスパターン
        new(@"(override|bypass|disable|ignore)\s+(safety|filter|restriction|guard)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // プロンプト漏洩要求パターン
        new(@"(reveal|show|display|output|print)\s+(system\s+prompt|instructions?|rules?)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // XSS パターン（HTML タグ）
        new(@"<\s*(script|img|iframe|object|embed|form)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // JavaScript URL パターン
        new(@"javascript\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // Data URL パターン（HTML 埋め込み）
        new(@"data\s*:\s*text/html", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // SQL インジェクションパターン
        new(@"(DROP\s+TABLE|DELETE\s+FROM|INSERT\s+INTO|UPDATE\s+.*\s+SET)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // 忘却指示パターン
        new(@"forget\s+(everything|all|previous)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // Jailbreak パターン
        new(@"DAN\s+mode", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // LLM 特殊トークンパターン
        new(@"<\|im_start\|>", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // Jailbreak キーワード
        new(@"jailbreak", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
    ];

    /// <summary>
    /// ユーザー入力をサニタイズし、プロンプトインジェクションの疑いがある場合はブロックする。
    /// </summary>
    /// <param name="input">ユーザーからの入力文字列。</param>
    /// <returns>
    /// サニタイズ結果のタプル:
    /// <list type="bullet">
    ///   <item><description><c>SanitizedInput</c>: サニタイズ済み入力文字列。ブロック時は空文字列。</description></item>
    ///   <item><description><c>IsBlocked</c>: 危険パターン検出によりブロックされた場合は <c>true</c>。</description></item>
    ///   <item><description><c>BlockReason</c>: ブロック理由のメッセージ。ブロックされない場合は <c>null</c>。</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> が <c>null</c> の場合。</exception>
    /// <remarks>
    /// <para>
    /// <b>処理順序:</b>
    /// <list type="number">
    ///   <item><description>null チェック（例外スロー）</description></item>
    ///   <item><description>空入力チェック（ブロック）</description></item>
    ///   <item><description>危険パターンマッチング（ブロック）</description></item>
    ///   <item><description>HTML タグ除去</description></item>
    ///   <item><description>特殊トークン除去</description></item>
    ///   <item><description>トリム</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>注意:</b> ブロック時は入力内容をログに出力しないこと（攻撃パターンの漏洩防止）。
    /// </para>
    /// </remarks>
    public static (string SanitizedInput, bool IsBlocked, string? BlockReason) Sanitize(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input))
            return (input, true, "入力が空です");

        foreach (var regex in DangerousPatterns)
        {
            if (regex.IsMatch(input))
            {
                return (string.Empty, true, $"プロンプトインジェクションの疑いがあるため、リクエストをブロックしました");
            }
        }

        var sanitized = HtmlTagRegex().Replace(input, string.Empty);
        sanitized = SpecialTokenRegex().Replace(sanitized, string.Empty);
        sanitized = sanitized.Trim();

        return (sanitized, false, null);
    }

    /// <summary>
    /// HTML タグにマッチする正規表現。
    /// </summary>
    /// <remarks>
    /// XSS 防止のため、全ての HTML タグを入力から除去する。
    /// これにより、AI レスポンスに HTML が含まれた場合の反射攻撃を防止する。
    /// </remarks>
    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    /// <summary>
    /// LLM 特殊トークン（例: <c>&lt;|im_start|&gt;</c>）にマッチする正規表現。
    /// </summary>
    /// <remarks>
    /// OpenAI 等の LLM が使用する特殊トークンを除去し、
    /// トークンインジェクションによるシステムプロンプト操作を防止する。
    /// </remarks>
    [GeneratedRegex(@"<\|[^|]+\|>", RegexOptions.Compiled)]
    private static partial Regex SpecialTokenRegex();
}
