namespace AuthService.Services.Interfaces;

/// <summary>
/// 暗号化サービスのインターフェース。
/// 機密データ（MFAシークレットなど）の暗号化と復号を提供します。
/// </summary>
/// <remarks>
/// セキュリティ考慮事項:
/// <list type="bullet">
///   <item>暗号化キーは安全に管理され、アプリケーションコードに含めないでください</item>
///   <item>対称鍵暗号化（AES-256など）を使用することを推奨します</item>
///   <item>復号された平文はメモリ上での保持を最小限にし、使用後はクリアしてください</item>
///   <item>暗号化キーのローテーションメカニズムを実装してください</item>
/// </list>
/// </remarks>
public interface IEncryptionService
{
    /// <summary>
    /// 平文を暗号化します。
    /// </summary>
    /// <param name="plainText">暗号化する平文。</param>
    /// <returns>Base64エンコードされた暗号文。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plainText"/> が null の場合。</exception>
    /// <remarks>
    /// セキュリティ:
    /// <list type="bullet">
    ///   <item>MFAシークレットキーなど、データベースに保存する機密情報の暗号化に使用します</item>
    ///   <item>暗号化処理は暗号論的に安全なアルゴリズムを使用します</item>
    /// </list>
    /// </remarks>
    string Encrypt(string plainText);

    /// <summary>
    /// 暗号文を復号します。
    /// </summary>
    /// <param name="cipherText">Base64エンコードされた暗号文。</param>
    /// <returns>復号された平文。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cipherText"/> が null の場合。</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">復号に失敗した場合。</exception>
    /// <remarks>
    /// セキュリティ:
    /// <list type="bullet">
    ///   <item>復号された平文は使用後速やかにメモリから削除してください</item>
    ///   <item>復号エラーの詳細はログに出力せず、攻撃者への情報漏洩を防止します</item>
    /// </list>
    /// </remarks>
    string Decrypt(string cipherText);
}
