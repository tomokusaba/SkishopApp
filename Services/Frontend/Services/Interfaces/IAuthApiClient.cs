using Frontend.DTOs;
using Frontend.Models;

namespace Frontend.Services.Interfaces;

/// <summary>
/// 認証 API クライアントインターフェース（§9.1 全エンドポイント対応）
/// BFF パターン: サーバーサイドから API Gateway 経由で認証サービスに通信
/// </summary>
public interface IAuthApiClient
{
    Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<MfaVerifyResponse> VerifyMfaAsync(string sessionToken, string code, CancellationToken ct = default);
    Task RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task ResendEmailVerificationAsync(string email, CancellationToken ct = default);
    Task<EmailVerifyResult> VerifyEmailAsync(string token, CancellationToken ct = default);
    Task RequestPasswordResetAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default);
    Task<MfaSetupResponse> SetupMfaAsync(string password, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task MergeCartAsync(CancellationToken ct = default);
}
