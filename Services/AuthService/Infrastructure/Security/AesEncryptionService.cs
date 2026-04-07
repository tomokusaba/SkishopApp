using System.Security.Cryptography;
using System.Text;
using AuthService.Services.Interfaces;

namespace AuthService.Infrastructure.Security;

/// <summary>
/// AES-GCM を使用したデータ暗号化サービスの実装。
/// </summary>
/// <remarks>
/// <para>
/// このクラスは、AES-256-GCM（Galois/Counter Mode）を使用して機密データを暗号化・復号します。
/// GCM モードは認証付き暗号化（AEAD）を提供し、データの機密性と完全性の両方を保証します。
/// </para>
/// <para>
/// <strong>暗号化アルゴリズム:</strong>
/// <list type="bullet">
///   <item><term>アルゴリズム</term><description>AES-256-GCM</description></item>
///   <item><term>鍵長</term><description>256 ビット（32 バイト）</description></item>
///   <item><term>ノンス長</term><description>96 ビット（12 バイト）</description></item>
///   <item><term>認証タグ長</term><description>128 ビット（16 バイト）</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>暗号文フォーマット:</strong>
/// <code>
/// [ノンス (12 bytes)] + [認証タグ (16 bytes)] + [暗号化データ (可変長)]
/// </code>
/// 全体は Base64 エンコードされます。
/// </para>
/// <para>
/// <strong>セキュリティ考慮事項:</strong>
/// <list type="bullet">
///   <item>各暗号化操作で新しいランダムノンスが生成されます</item>
///   <item>同じ平文でも毎回異なる暗号文が生成されます</item>
///   <item>認証タグにより改ざん検知が可能です</item>
///   <item>暗号鍵は設定から読み込まれ、Base64 デコードされます</item>
/// </list>
/// </para>
/// <para>
/// <strong>鍵の設定:</strong>
/// 暗号鍵は <c>Encryption:Key</c> 設定キーから読み込まれます。
/// 本番環境では、Azure Key Vault などのシークレット管理サービスを使用してください。
/// <code>
/// dotnet user-secrets set "Encryption:Key" "$(openssl rand -base64 32)"
/// </code>
/// </para>
/// <para>
/// <strong>使用例:</strong>
/// <list type="bullet">
///   <item>MFA 秘密鍵（TOTP シード）の暗号化</item>
///   <item>OAuth リフレッシュトークンの暗号化</item>
///   <item>その他の機密データの暗号化</item>
/// </list>
/// </para>
/// </remarks>
public class AesEncryptionService : IEncryptionService
{
    /// <summary>
    /// 暗号化に使用する 256 ビット鍵。
    /// </summary>
    private readonly byte[] _key;

    /// <summary>
    /// <see cref="AesEncryptionService"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="configuration">暗号鍵を取得するための設定。</param>
    /// <exception cref="InvalidOperationException">
    /// 暗号鍵が設定されていない場合、または鍵長が 256 ビットでない場合にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// 暗号鍵は <c>Encryption:Key</c> 設定キーから Base64 デコードされます。
    /// 鍵は正確に 32 バイト（256 ビット）である必要があります。
    /// </para>
    /// </remarks>
    public AesEncryptionService(IConfiguration configuration)
    {
        var keyString = configuration["Encryption:Key"]
            ?? throw new InvalidOperationException("Encryption key is not configured");
        _key = Convert.FromBase64String(keyString);
        if (_key.Length != 32)
            throw new InvalidOperationException("Encryption key must be 256 bits (32 bytes)");
    }

    /// <summary>
    /// 平文を AES-256-GCM で暗号化します。
    /// </summary>
    /// <param name="plainText">暗号化する平文。</param>
    /// <returns>Base64 エンコードされた暗号文（ノンス + タグ + 暗号化データ）。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="plainText"/> が null の場合にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>暗号化プロセス:</strong>
    /// <list type="number">
    ///   <item><description>平文を UTF-8 バイト配列に変換</description></item>
    ///   <item><description>12 バイトのランダムノンスを生成</description></item>
    ///   <item><description>AES-GCM で暗号化し、16 バイトの認証タグを生成</description></item>
    ///   <item><description>ノンス + タグ + 暗号文を結合して Base64 エンコード</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public string Encrypt(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes
        var cipherBytes = new byte[plainBytes.Length];

        using var aesGcm = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Format: nonce (12) + tag (16) + ciphertext
        var result = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, nonce.Length);
        cipherBytes.CopyTo(result, nonce.Length + tag.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// AES-256-GCM で暗号化されたデータを復号します。
    /// </summary>
    /// <param name="cipherText">Base64 エンコードされた暗号文。</param>
    /// <returns>復号された平文。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cipherText"/> が null の場合にスローされます。
    /// </exception>
    /// <exception cref="CryptographicException">
    /// 復号に失敗した場合（改ざん検知、鍵の不一致など）にスローされます。
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>復号プロセス:</strong>
    /// <list type="number">
    ///   <item><description>Base64 デコードして元のバイト配列を復元</description></item>
    ///   <item><description>ノンス（先頭 12 バイト）を抽出</description></item>
    ///   <item><description>認証タグ（次の 16 バイト）を抽出</description></item>
    ///   <item><description>暗号化データ（残り）を抽出</description></item>
    ///   <item><description>AES-GCM で復号（認証タグの検証を含む）</description></item>
    ///   <item><description>UTF-8 文字列に変換</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>セキュリティ:</strong>
    /// 認証タグの検証により、暗号文が改ざんされている場合は
    /// <see cref="CryptographicException"/> がスローされます。
    /// </para>
    /// </remarks>
    public string Decrypt(string cipherText)
    {
        ArgumentNullException.ThrowIfNull(cipherText);

        var fullCipher = Convert.FromBase64String(cipherText);
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize = AesGcm.TagByteSizes.MaxSize;

        var nonce = fullCipher[..nonceSize];
        var tag = fullCipher[nonceSize..(nonceSize + tagSize)];
        var cipherBytes = fullCipher[(nonceSize + tagSize)..];
        var plainBytes = new byte[cipherBytes.Length];

        using var aesGcm = new AesGcm(_key, tagSize);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
