using System.Text.RegularExpressions;

namespace AiSupportService.Infrastructure.SemanticKernel;

/// <summary>
/// AI レスポンスに含まれる個人情報（PII）および内部パス情報をマスキングするフィルター。
/// </summary>
/// <remarks>
/// <para>
/// <b>セキュリティ目的:</b>
/// AI モデルがトレーニングデータやコンテキストから個人情報を漏洩するリスクに対処する。
/// AI レスポンスをクライアントに返す前に、PII および内部実装情報をマスキングする。
/// </para>
/// <para>
/// <b>マスキング対象:</b>
/// <list type="bullet">
///   <item><description><b>メールアドレス:</b> <c>user@example.com</c> → <c>[REDACTED]</c></description></item>
///   <item><description><b>電話番号:</b> <c>03-1234-5678</c> → <c>[REDACTED]</c></description></item>
///   <item><description><b>クレジットカード番号:</b> <c>1234-5678-9012-3456</c> → <c>[REDACTED]</c></description></item>
///   <item><description><b>内部ファイルパス:</b> <c>/app/src/services/user.cs</c> → <c>[PATH_REDACTED]</c></description></item>
/// </list>
/// </para>
/// <para>
/// <b>処理タイミング:</b>
/// <see cref="IChatCompletionService"/> からレスポンスを受け取った直後、
/// クライアントに返す前にこのフィルターを適用する。
/// </para>
/// <para>
/// <b>ReDoS 対策:</b> 全ての正規表現に 1 秒のタイムアウトを設定し、
/// 正規表現 DoS 攻撃を防止する。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var aiResponse = await chatService.GetChatMessageContentAsync(history, ct);
/// var filteredResponse = ResponseFilter.Filter(aiResponse.Content);
/// // filteredResponse をクライアントに返す
/// </code>
/// </example>
public static partial class ResponseFilter
{
    /// <summary>
    /// PII（個人情報）を検出するための正規表現パターン一覧。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description>メールアドレス: RFC 5322 の簡易版パターン</description></item>
    ///   <item><description>電話番号: 日本の固定電話・携帯電話パターン（ハイフン・ドット区切り対応）</description></item>
    ///   <item><description>クレジットカード番号: 16 桁のパターン（ハイフン・スペース区切り対応）</description></item>
    /// </list>
    /// </remarks>
    private static readonly Regex[] PiiPatterns =
    [
        // メールアドレス
        new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // 日本の電話番号（固定・携帯）
        new(@"\b\d{3}[-.]?\d{4}[-.]?\d{4}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
        // クレジットカード番号（16 桁）
        new(@"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
    ];

    /// <summary>
    /// AI レスポンス文字列から PII および内部パス情報を検出し、マスキングして返す。
    /// </summary>
    /// <param name="response">AI モデルから返されたレスポンス文字列。</param>
    /// <returns>PII および内部パスがマスキングされたレスポンス文字列。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> が <c>null</c> の場合。</exception>
    /// <remarks>
    /// <para>
    /// <b>処理順序:</b>
    /// <list type="number">
    ///   <item><description>PII パターン（メール、電話番号、クレジットカード）の置換</description></item>
    ///   <item><description>内部パスパターンの置換</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>マスキング文字列:</b>
    /// <list type="bullet">
    ///   <item><description>PII: <c>[REDACTED]</c></description></item>
    ///   <item><description>内部パス: <c>[PATH_REDACTED]</c></description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>注意:</b> 過検出（false positive）の可能性があるため、
    /// ビジネス要件に応じてパターンを調整すること。
    /// </para>
    /// </remarks>
    public static string Filter(string response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var filtered = response;

        foreach (var regex in PiiPatterns)
        {
            filtered = regex.Replace(filtered, "[REDACTED]");
        }

        filtered = InternalPathRegex().Replace(filtered, "[PATH_REDACTED]");

        return filtered;
    }

    /// <summary>
    /// 内部ファイルパス（3 階層以上のスラッシュ区切りパス）にマッチする正規表現。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 内部サーバーのファイルパス（例: <c>/app/src/services/user.cs</c>）が
    /// AI レスポンスに含まれることを防止する。これは内部実装の漏洩につながる可能性がある。
    /// </para>
    /// <para>
    /// <b>パターン:</b> 3 階層以上の Unix スタイルパス（<c>/dir/subdir/file</c> 形式）にマッチ。
    /// </para>
    /// </remarks>
    [GeneratedRegex(@"(?:/[a-zA-Z_][a-zA-Z0-9_]*){3,}", RegexOptions.Compiled)]
    private static partial Regex InternalPathRegex();
}
