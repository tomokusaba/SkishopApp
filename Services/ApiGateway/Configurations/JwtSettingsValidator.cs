// ─────────────────────────────────────────────────────────────
// JwtSettingsValidator — JWT 設定のカスタム検証
// Data Annotations だけでは表現できない相互排他・条件付き必須
// のバリデーションロジックを提供する。
// ─────────────────────────────────────────────────────────────

using Microsoft.Extensions.Options;

namespace ApiGateway.Configurations;

/// <summary>
/// <see cref="JwtSettings"/> のカスタムバリデーター。
/// <see cref="JwtSettings.SigningKey"/> と <see cref="JwtSettings.Authority"/>
/// のいずれかが設定されていることを検証する。
/// </summary>
/// <remarks>
/// <see cref="IValidateOptions{TOptions}"/> を実装し、
/// <c>ValidateOnStart()</c> のタイミングで呼び出される。
/// </remarks>
public sealed class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    /// <summary>
    /// JWT 設定の相互排他バリデーションを実行する。
    /// SigningKey と Authority の両方が未設定の場合、起動を中断させる。
    /// </summary>
    /// <param name="name">オプション名（Named Options 使用時）。</param>
    /// <param name="options">検証対象の <see cref="JwtSettings"/>。</param>
    /// <returns>検証結果。失敗時はエラーメッセージを含む。</returns>
    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey) && string.IsNullOrWhiteSpace(options.Authority))
        {
            return ValidateOptionsResult.Fail(
                "Jwt:SigningKey または Jwt:Authority のいずれかを設定してください。" +
                "SigningKey は HS256（対称鍵）、Authority は RS256/ES256（JWKS）に対応します。");
        }

        // SigningKey が設定されている場合、最低 32 バイト（256 bit）を要求
        if (!string.IsNullOrWhiteSpace(options.SigningKey) && options.SigningKey.Length < 32)
        {
            return ValidateOptionsResult.Fail(
                "Jwt:SigningKey は最低 32 文字（256 bit）以上の長さが必要です。" +
                "セキュリティ上、十分なエントロピーを持つ鍵を使用してください。");
        }

        return ValidateOptionsResult.Success;
    }
}
