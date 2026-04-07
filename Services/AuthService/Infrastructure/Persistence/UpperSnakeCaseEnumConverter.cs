using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AuthService.Infrastructure.Persistence;

/// <summary>
/// C# の enum 値と UPPER_SNAKE_CASE 文字列間の変換を行う EF Core 値コンバーター。
/// </summary>
/// <typeparam name="TEnum">変換対象の enum 型。</typeparam>
/// <remarks>
/// <para>
/// このコンバーターは、C# の PascalCase enum 値をデータベースの UPPER_SNAKE_CASE 文字列に
/// 変換し、その逆変換も行います。これにより、C# のコーディング規約と SQL のコーディング規約の
/// 両方に準拠できます。
/// </para>
/// <para>
/// <strong>変換例:</strong>
/// <list type="bullet">
///   <item><c>PendingVerification</c> → <c>PENDING_VERIFICATION</c></item>
///   <item><c>Active</c> → <c>ACTIVE</c></item>
///   <item><c>AccountLocked</c> → <c>ACCOUNT_LOCKED</c></item>
/// </list>
/// </para>
/// <para>
/// <strong>使用例（AuthDbContext.OnModelCreating）:</strong>
/// <code>
/// entity.Property(u => u.Status)
///     .HasConversion(new UpperSnakeCaseEnumConverter&lt;UserStatus&gt;())
///     .HasMaxLength(50);
/// </code>
/// </para>
/// <para>
/// <strong>正規表現パターン:</strong>
/// <c>(?&lt;=[a-z0-9])([A-Z])|(?&lt;=[A-Z])([A-Z](?=[a-z]))</c>
/// <list type="bullet">
///   <item>小文字・数字の後の大文字を検出（例: <c>loginAttempt</c> → <c>login_Attempt</c>）</item>
///   <item>連続する大文字の最後を検出（例: <c>HTTPRequest</c> → <c>HTTP_Request</c>）</item>
/// </list>
/// </para>
/// </remarks>
public partial class UpperSnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    /// <summary>
    /// <see cref="UpperSnakeCaseEnumConverter{TEnum}"/> の新しいインスタンスを初期化します。
    /// </summary>
    public UpperSnakeCaseEnumConverter()
        : base(
            v => ToUpperSnakeCase(v.ToString()),
            v => Enum.Parse<TEnum>(FromUpperSnakeCase(v), ignoreCase: true))
    {
    }

    /// <summary>
    /// PascalCase 文字列を UPPER_SNAKE_CASE に変換します。
    /// </summary>
    /// <param name="input">変換する PascalCase 文字列。</param>
    /// <returns>UPPER_SNAKE_CASE 形式の文字列。</returns>
    private static string ToUpperSnakeCase(string input)
        => PascalToSnakeRegex().Replace(input, "_$1$2").TrimStart('_').ToUpperInvariant();

    /// <summary>
    /// UPPER_SNAKE_CASE 文字列を PascalCase に変換します。
    /// </summary>
    /// <param name="input">変換する UPPER_SNAKE_CASE 文字列。</param>
    /// <returns>PascalCase 形式の文字列。</returns>
    private static string FromUpperSnakeCase(string input)
    {
        var parts = input.Split('_');
        return string.Concat(parts.Select(p =>
            p.Length > 0 ? char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant() : p));
    }

    /// <summary>
    /// PascalCase から snake_case への変換に使用する正規表現を生成します。
    /// </summary>
    /// <returns>コンパイル済みの正規表現。</returns>
    /// <remarks>
    /// ソースジェネレーターにより、コンパイル時に正規表現がコンパイルされます。
    /// </remarks>
    [GeneratedRegex(@"(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z](?=[a-z]))")]
    private static partial Regex PascalToSnakeRegex();
}
