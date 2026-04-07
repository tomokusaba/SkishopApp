using System.Text.RegularExpressions;

namespace MailSendService.Models.ValueObjects;

/// <summary>
/// メールアドレスを表す Value Object。
/// </summary>
/// <remarks>
/// <para>DDD の Value Object パターンに従い、不変性と値ベースの等価性を保証する。</para>
/// <para>メールアドレスの形式バリデーションと正規化（小文字化）を行う。</para>
/// <para>GDPR 準拠のマスキング機能を提供する。</para>
/// </remarks>
public readonly partial record struct EmailAddress
{
    /// <summary>正規化されたメールアドレス値。</summary>
    public string Value { get; }

    /// <summary>
    /// 新しい <see cref="EmailAddress"/> インスタンスを作成する。
    /// </summary>
    /// <param name="value">メールアドレス文字列。</param>
    /// <exception cref="ArgumentException">
    /// メールアドレスが null、空、または無効な形式の場合。
    /// </exception>
    public EmailAddress(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        if (!IsValidFormat(value))
            throw new ArgumentException($"無効なメールアドレス形式です: {MaskForError(value)}", nameof(value));

        Value = value.ToLowerInvariant();
    }

    /// <summary>
    /// ログ出力・API レスポンス用にマスキングされたメールアドレスを取得する。
    /// </summary>
    /// <remarks>
    /// ローカルパートの先頭 1 文字を残し、残りを "***" に置換する。
    /// 例: "user@example.com" → "u***@example.com"
    /// </remarks>
    public string Masked
    {
        get
        {
            var atIndex = Value.IndexOf('@');
            return atIndex > 0
                ? $"{Value[0]}***{Value[atIndex..]}"
                : "***";
        }
    }

    /// <summary>
    /// ドメイン部分のみを取得する。
    /// </summary>
    /// <remarks>
    /// 例: "user@example.com" → "example.com"
    /// </remarks>
    public string Domain
    {
        get
        {
            var atIndex = Value.IndexOf('@');
            return atIndex >= 0 ? Value[(atIndex + 1)..] : string.Empty;
        }
    }

    /// <summary>
    /// ローカルパート（@ より前の部分）を取得する。
    /// </summary>
    /// <remarks>
    /// 例: "user@example.com" → "user"
    /// </remarks>
    public string LocalPart
    {
        get
        {
            var atIndex = Value.IndexOf('@');
            return atIndex > 0 ? Value[..atIndex] : Value;
        }
    }

    /// <summary>
    /// 文字列への暗黙的な変換演算子。
    /// </summary>
    /// <param name="email">変換元の <see cref="EmailAddress"/>。</param>
    /// <returns>メールアドレス文字列。</returns>
    public static implicit operator string(EmailAddress email) => email.Value;

    /// <summary>
    /// 文字列からの明示的な変換演算子。
    /// </summary>
    /// <param name="value">変換元の文字列。</param>
    /// <returns>新しい <see cref="EmailAddress"/> インスタンス。</returns>
    /// <exception cref="ArgumentException">無効なメールアドレス形式の場合。</exception>
    public static explicit operator EmailAddress(string value) => new(value);

    /// <summary>
    /// 文字列からの変換を試行する。
    /// </summary>
    /// <param name="value">変換元の文字列。</param>
    /// <param name="result">変換結果。</param>
    /// <returns>変換が成功した場合は <c>true</c>。</returns>
    public static bool TryCreate(string? value, out EmailAddress result)
    {
        if (string.IsNullOrWhiteSpace(value) || !IsValidFormat(value))
        {
            result = default;
            return false;
        }

        result = new EmailAddress(value);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// メールアドレスの形式が有効かどうかを検証する。
    /// </summary>
    /// <param name="email">検証対象のメールアドレス。</param>
    /// <returns>有効な形式の場合は <c>true</c>。</returns>
    private static bool IsValidFormat(string email)
        => !string.IsNullOrWhiteSpace(email)
           && email.Contains('@')
           && email.Contains('.')
           && EmailRegex().IsMatch(email);

    /// <summary>
    /// エラーメッセージ用にメールアドレスをマスキングする。
    /// </summary>
    private static string MaskForError(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex > 0
            ? $"{email[0]}***{email[atIndex..]}"
            : "***";
    }

    /// <summary>
    /// メールアドレス形式の正規表現（RFC 5322 簡易版）。
    /// </summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();
}
