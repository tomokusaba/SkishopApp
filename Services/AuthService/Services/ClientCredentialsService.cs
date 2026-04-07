using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Configurations;
using AuthService.DTOs.Requests;
using AuthService.DTOs.Responses;
using AuthService.Exceptions;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Services;

/// <summary>
/// <see cref="IClientCredentialsService"/> の実装クラス。
/// OAuth 2.0 Client Credentials Grant によるサービス間認証のトークン発行を担当します。
/// </summary>
/// <remarks>
/// セキュリティ機能:
/// <list type="bullet">
///   <item>クライアントシークレットのハッシュ検証</item>
///   <item>スコープベースのアクセス制御</item>
///   <item>クライアントの有効/無効状態チェック</item>
///   <item>認証失敗のセキュリティログ記録</item>
/// </list>
/// </remarks>
/// <param name="oAuthClientRepository">OAuthクライアントリポジトリ。</param>
/// <param name="passwordHasher">パスワードハッシャー（クライアントシークレット検証用）。</param>
/// <param name="securityService">セキュリティサービス。</param>
/// <param name="timeProvider">時刻プロバイダー。</param>
/// <param name="jwtSettings">JWT設定。</param>
/// <param name="logger">ロガー。</param>
public class ClientCredentialsService(
    IOAuthClientRepository oAuthClientRepository,
    IPasswordHasher<User> passwordHasher,
    ISecurityService securityService,
    TimeProvider timeProvider,
    IOptions<JwtSettings> jwtSettings,
    ILogger<ClientCredentialsService> logger) : IClientCredentialsService
{
    private readonly JwtSettings _jwtSettings = jwtSettings.Value;

    /// <inheritdoc />
    public async Task<ClientCredentialsResponse> IssueTokenAsync(
        ClientCredentialsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GrantType != "client_credentials")
        {
            throw new BusinessException("サポートされていないグラントタイプです。'client_credentials' を指定してください。");
        }

        var client = await oAuthClientRepository.FindByClientIdAsync(request.ClientId, ct)
            ?? throw new UnauthorizedException("無効なクライアント認証情報です");

        if (!client.IsActive)
        {
            throw new BusinessException("クライアントが無効化されています");
        }

        var verifyResult = passwordHasher.VerifyHashedPassword(null!, client.ClientSecretHash, request.ClientSecret);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            await securityService.LogSecurityEventAsync(
                null, "CLIENT_AUTH_FAILED", null, null,
                $"クライアント認証失敗: ClientId={request.ClientId}", ct);
            throw new UnauthorizedException("無効なクライアント認証情報です");
        }

        var allowedScopes = client.AllowedScopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var requestedScopes = request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var scope in requestedScopes)
        {
            if (!allowedScopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
            {
                throw new BusinessException($"スコープ '{scope}' は許可されていません");
            }
        }

        var keyString = _jwtSettings.SecretKey;
        if (string.IsNullOrEmpty(keyString))
            throw new InvalidOperationException("JWT SecretKey is not configured");
        if (keyString.Length < 32)
            throw new InvalidOperationException("JWT SecretKey must be at least 32 characters for HMAC-SHA256");
        var keyBytes = Encoding.UTF8.GetBytes(keyString);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var now = timeProvider.GetUtcNow();
        var expiresIn = _jwtSettings.AccessExpirationSeconds;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, client.ClientId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("client_id", client.ClientId),
            new("scope", request.Scope)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddSeconds(expiresIn).UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        await securityService.LogSecurityEventAsync(
            null, "CLIENT_TOKEN_ISSUED", null, null,
            $"クライアントトークン発行: ClientId={request.ClientId}", ct);

        logger.LogInformation("クライアントトークン発行: ClientId={ClientId}, Scope={Scope}",
            request.ClientId, request.Scope);

        return new ClientCredentialsResponse(accessToken, "Bearer", expiresIn, request.Scope);
    }
}
