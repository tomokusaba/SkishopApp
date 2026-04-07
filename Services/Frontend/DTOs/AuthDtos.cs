namespace Frontend.DTOs;

public record LoginRequest(string Email, string Password);
public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    LoginUserInfo User,
    bool MfaRequired = false,
    string? SessionToken = null);
public record LoginUserInfo(string Id, string FirstName, string LastName, string Role);
public record MfaVerifyRequest(string SessionToken, string Code);
public record MfaVerifyResponse(string AccessToken, string RefreshToken, int ExpiresIn, LoginUserInfo User);
public record RegisterRequest(string Email, string Password, string FirstName, string LastName, string Username);
public record EmailVerifyResult(string Status);
public record MfaSetupResponse(string QrCodeUrl, string SecretKey, List<string> BackupCodes);
