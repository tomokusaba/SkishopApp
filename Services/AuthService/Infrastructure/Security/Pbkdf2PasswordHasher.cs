using AuthService.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure.Security;

/// <summary>
/// PBKDF2 を使用したパスワードハッシュサービスの実装。
/// </summary>
/// <remarks>
/// <para>
/// このクラスは、ASP.NET Core Identity の <see cref="PasswordHasher{TUser}"/> をラップし、
/// OWASP の推奨に従った強力なパスワードハッシュを提供します。
/// </para>
/// <para>
/// <strong>ハッシュアルゴリズム:</strong>
/// <list type="bullet">
///   <item><term>アルゴリズム</term><description>PBKDF2-HMAC-SHA256</description></item>
///   <item><term>イテレーション回数</term><description>600,000 回</description></item>
///   <item><term>ソルト長</term><description>128 ビット（16 バイト）</description></item>
///   <item><term>派生鍵長</term><description>256 ビット（32 バイト）</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>イテレーション回数の根拠:</strong>
/// OWASP Password Storage Cheat Sheet（2024年版）では、PBKDF2-HMAC-SHA256 に対して
/// 最低 600,000 イテレーションを推奨しています。これは、現代の GPU による
/// ブルートフォース攻撃に対する耐性を確保するために必要な値です。
/// </para>
/// <para>
/// <strong>ハッシュフォーマット:</strong>
/// ASP.NET Core Identity v3 フォーマットが使用されます:
/// <code>
/// [バージョン (1 byte)] + [アルゴリズム情報] + [ソルト (16 bytes)] + [派生鍵 (32 bytes)]
/// </code>
/// 全体は Base64 エンコードされます。
/// </para>
/// <para>
/// <strong>セキュリティ考慮事項:</strong>
/// <list type="bullet">
///   <item>各パスワードに対して一意のランダムソルトが使用されます</item>
///   <item>同じパスワードでも異なるハッシュ値が生成されます</item>
///   <item>レインボーテーブル攻撃に対する耐性があります</item>
///   <item>高いイテレーション回数により、GPU ベースの攻撃を遅延させます</item>
/// </list>
/// </para>
/// <para>
/// <strong>パフォーマンス:</strong>
/// 600,000 イテレーションでのハッシュ計算には約 200-500ms かかります。
/// これは意図的な設計であり、攻撃者のコストを増大させます。
/// </para>
/// </remarks>
public class Pbkdf2PasswordHasher : IPasswordHasher<User>
{
    /// <summary>
    /// 内部で使用する ASP.NET Core Identity のパスワードハッシャー。
    /// </summary>
    /// <remarks>
    /// 600,000 イテレーションで構成されています。
    /// </remarks>
    private readonly PasswordHasher<User> _inner = new(new OptionsWrapper<PasswordHasherOptions>(
        new PasswordHasherOptions { IterationCount = 600_000 }));

    /// <summary>
    /// パスワードをハッシュ化します。
    /// </summary>
    /// <param name="user">ハッシュ対象のユーザー（現在は使用されていません）。</param>
    /// <param name="password">ハッシュ化するパスワード。</param>
    /// <returns>Base64 エンコードされたパスワードハッシュ。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="password"/> が null の場合にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// 生成されるハッシュには、バージョン情報、アルゴリズム情報、ソルト、
    /// および派生鍵が含まれます。これにより、将来のアルゴリズム変更に対応できます。
    /// </para>
    /// </remarks>
    public string HashPassword(User? user, string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return _inner.HashPassword(user!, password);
    }

    /// <summary>
    /// ハッシュ化されたパスワードと提供されたパスワードを検証します。
    /// </summary>
    /// <param name="user">検証対象のユーザー（現在は使用されていません）。</param>
    /// <param name="hashedPassword">データベースに保存されているハッシュ。</param>
    /// <param name="providedPassword">検証するパスワード。</param>
    /// <returns>
    /// 検証結果を示す <see cref="PasswordVerificationResult"/>:
    /// <list type="bullet">
    ///   <item><see cref="PasswordVerificationResult.Success"/> - パスワードが一致</item>
    ///   <item><see cref="PasswordVerificationResult.SuccessRehashNeeded"/> - パスワードが一致するが、再ハッシュが推奨される（古いアルゴリズムが使用されている場合）</item>
    ///   <item><see cref="PasswordVerificationResult.Failed"/> - パスワードが一致しない</item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="hashedPassword"/> または <paramref name="providedPassword"/> が null の場合にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// <see cref="PasswordVerificationResult.SuccessRehashNeeded"/> が返された場合、
    /// 呼び出し元はユーザーのパスワードを現在のアルゴリズムで再ハッシュして
    /// データベースを更新する必要があります。
    /// </para>
    /// <para>
    /// <strong>タイミング攻撃対策:</strong>
    /// 内部の <see cref="PasswordHasher{TUser}"/> は定数時間比較を使用しており、
    /// タイミングサイドチャネル攻撃に対する耐性があります。
    /// </para>
    /// </remarks>
    public PasswordVerificationResult VerifyHashedPassword(
        User? user,
        string hashedPassword,
        string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(hashedPassword);
        ArgumentNullException.ThrowIfNull(providedPassword);
        return _inner.VerifyHashedPassword(user!, hashedPassword, providedPassword);
    }
}
