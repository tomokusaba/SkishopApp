using Frontend.DTOs;
using Frontend.Models;
using Frontend.Services.Interfaces;

namespace Frontend.Services;

/// <summary>
/// 認証 API クライアント（§9.1 全エンドポイント対応）
/// BFF パターン: サーバーサイドから API Gateway 経由で認証サービスに通信
/// </summary>
public class AuthApiClient(
    IApiGatewayClient apiClient,
    ILogger<AuthApiClient> logger) : IAuthApiClient
{
    public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var response = await apiClient.PostAsync<LoginRequest, LoginResponse>(
            "/api/v1/auth/login",
            new LoginRequest(email, password), ct)
            ?? throw new InvalidOperationException("ログインレスポンスのデシリアライズに失敗");
        return response;
    }

    public async Task<MfaVerifyResponse> VerifyMfaAsync(string sessionToken, string code, CancellationToken ct = default)
    {
        var response = await apiClient.PostAsync<MfaVerifyRequest, MfaVerifyResponse>(
            "/api/v1/auth/mfa/verify",
            new MfaVerifyRequest(sessionToken, code), ct)
            ?? throw new InvalidOperationException("MFA 検証レスポンスのデシリアライズに失敗");
        return response;
    }

    public async Task RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        await apiClient.PostAsync<RegisterRequest, object>("/api/v1/auth/users", request, ct);
    }

    public async Task ResendEmailVerificationAsync(string email, CancellationToken ct = default)
    {
        await apiClient.PostAsync<object, object>("/api/v1/auth/email/resend", new { Email = email }, ct);
    }

    public async Task<EmailVerifyResult> VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        var response = await apiClient.PostAsync<object, EmailVerifyResult>(
            "/api/v1/auth/email/verify",
            new { Token = token }, ct)
            ?? new EmailVerifyResult("INVALID");
        return response;
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken ct = default)
    {
        await apiClient.PostAsync<object, object>("/api/v1/auth/password/reset-request",
            new { Email = email }, ct);
    }

    public async Task ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default)
    {
        await apiClient.PostAsync<object, object>("/api/v1/auth/password/reset",
            new { Token = token, NewPassword = newPassword }, ct);
    }

    public async Task<MfaSetupResponse> SetupMfaAsync(string password, CancellationToken ct = default)
    {
        var response = await apiClient.PostAsync<object, MfaSetupResponse>(
            "/api/v1/auth/mfa/setup",
            new { Password = password }, ct)
            ?? throw new InvalidOperationException("MFA セットアップレスポンスのデシリアライズに失敗");
        return response;
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        try
        {
            await apiClient.PostAsync<object, object>("/api/v1/auth/logout", new { }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ログアウト API 呼び出しエラー");
        }
    }

    public async Task MergeCartAsync(CancellationToken ct = default)
    {
        try
        {
            await apiClient.PostAsync<object, object>("/api/v1/cart/merge", new { }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "カートマージ API 呼び出しエラー");
        }
    }
}

