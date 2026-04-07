using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthService.Configurations;
using AuthService.DTOs.Responses;
using AuthService.Models;
using AuthService.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Services;

/// <summary>
/// <see cref="IJwtTokenService"/> の実装クラス。
/// JWTアクセストークンの生成、検証、無効化およびリフレッシュトークンの生成を担当します。
/// </summary>
/// <remarks>
/// セキュリティ機能:
/// <list type="bullet">
///   <item>HMAC-SHA256による署名</item>
///   <item>発行者・対象者・有効期限の検証</item>
///   <item>トークンブラックリストによる無効化管理</item>
///   <item>暗号学的に安全な乱数によるリフレッシュトークン生成</item>
/// </list>
/// </remarks>
/// <param name="jwtSettings">JWT設定。</param>
/// <param name="configuration">アプリケーション設定。</param>
/// <param name="timeProvider">時刻プロバイダー。</param>
/// <param name="tokenBlacklistService">トークンブラックリストサービス。</param>
/// <param name="logger">ロガー。</param>
public class JwtTokenService(
    IOptions<JwtSettings> jwtSettings,
    IConfiguration configuration,
    TimeProvider timeProvider,
    Infrastructure.Cache.ITokenBlacklistService tokenBlacklistService,
    ILogger<JwtTokenService> logger) : IJwtTokenService
{
    private readonly JwtSettings _jwtSettings = jwtSettings.Value;

    /// <inheritdoc />
    public string GenerateAccessToken(User user, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(sessionId);

        var keyString = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured");
        if (keyString.Length < 32)
            throw new InvalidOperationException("JWT SecretKey must be at least 32 characters for HMAC-SHA256");
        var keyBytes = Encoding.UTF8.GetBytes(keyString);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var now = timeProvider.GetUtcNow();
        var jti = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(ClaimTypes.Role, user.Role.ToString().ToUpperInvariant()),
            new("session_id", sessionId)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddSeconds(_jwtSettings.AccessExpirationSeconds).UtcDateTime,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        logger.LogInformation("アクセストークン生成: UserId={UserId}, Jti={Jti}", user.Id, jti);

        return tokenString;
    }

    /// <inheritdoc />
    public string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(randomBytes);
    }

    /// <inheritdoc />
    public Task<TokenValidationResponse> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var keyString = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured");
        if (keyString.Length < 32)
            throw new InvalidOperationException("JWT SecretKey must be at least 32 characters for HMAC-SHA256");
        var keyBytes = Encoding.UTF8.GetBytes(keyString);
        var securityKey = new SymmetricSecurityKey(keyBytes);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _jwtSettings.Issuer,
            ValidAudience = _jwtSettings.Audience,
            IssuerSigningKey = securityKey,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var role = principal.FindFirstValue(ClaimTypes.Role);
            var expiresAt = new DateTimeOffset(validatedToken.ValidTo, TimeSpan.Zero);

            return Task.FromResult(new TokenValidationResponse(true, userId, role, expiresAt));
        }
        catch (SecurityTokenException ex)
        {
            logger.LogWarning("トークン検証失敗: {Message}", ex.Message);
            return Task.FromResult(new TokenValidationResponse(false, null, null, null));
        }
    }

    /// <inheritdoc />
    public async Task RevokeTokenAsync(string tokenId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tokenId);

        var remaining = TimeSpan.FromSeconds(_jwtSettings.AccessExpirationSeconds);
        await tokenBlacklistService.BlacklistTokenAsync(tokenId, remaining, ct);

        logger.LogInformation("トークン無効化完了: TokenId={TokenId}", tokenId);
    }
}
