using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin;

[AllowAnonymous]
public class LoginModel(
    IHttpClientFactory httpClientFactory,
    ILogger<LoginModel> logger) : PageModel
{
    /// <summary>管理者ポータルへのログインが許可されるロール一覧。</summary>
    private static readonly HashSet<string> AllowedRoles = ["ADMIN", "MANAGER", "STAFF"];

    [BindProperty]
    [Required(ErrorMessage = "メールアドレスは必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "パスワードは必須です")]
    [StringLength(100, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "メールアドレスとパスワードを正しく入力してください";
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var loginRequest = new { Email, Password };
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("管理者ログイン失敗: Email={MaskedEmail}, StatusCode={StatusCode}",
                    MaskEmail(Email), response.StatusCode);
                ErrorMessage = "メールアドレスまたはパスワードが正しくありません";
                return Page();
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<AuthLoginResponse>(ct);
            if (loginResponse?.User is null)
            {
                ErrorMessage = "ログインレスポンスの解析に失敗しました";
                return Page();
            }

            // 管理者ロールチェック: ADMIN / MANAGER / STAFF のみ許可
            if (!AllowedRoles.Contains(loginResponse.User.Role))
            {
                logger.LogWarning("管理者ポータルへの権限不足: Email={MaskedEmail}, Role={Role}",
                    MaskEmail(Email), loginResponse.User.Role);
                ErrorMessage = "管理者権限がありません。管理者アカウントでログインしてください";
                return Page();
            }

            var displayName = $"{loginResponse.User.LastName} {loginResponse.User.FirstName}";
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, loginResponse.User.Id),
                new(ClaimTypes.Name, displayName),
                new(ClaimTypes.Email, Email),
                new(ClaimTypes.Role, loginResponse.User.Role),
                new("jwt_token", loginResponse.AccessToken)
            };

            var identity = new ClaimsIdentity(claims, "AdminCookies");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("AdminCookies", principal);

            logger.LogInformation("管理者ログイン成功: UserId={UserId}, Role={Role}",
                loginResponse.User.Id, loginResponse.User.Role);

            return RedirectToPage("/Admin/Index");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "ログインAPI接続エラー: {Message}", ex.Message);
            ErrorMessage = "認証サーバーに接続できません。しばらく経ってから再度お試しください";
            return Page();
        }
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1) return "***";
        return string.Concat(email.AsSpan(0, 1), "***", email.AsSpan(atIndex));
    }

    /// <summary>AuthService の POST /api/v1/auth/login レスポンス。</summary>
    public sealed record AuthLoginResponse(
        [property: JsonPropertyName("accessToken")] string AccessToken,
        [property: JsonPropertyName("refreshToken")] string RefreshToken,
        [property: JsonPropertyName("tokenType")] string TokenType,
        [property: JsonPropertyName("expiresIn")] int ExpiresIn,
        [property: JsonPropertyName("user")] AuthUserDto User);

    /// <summary>AuthService のユーザー情報 DTO。</summary>
    public sealed record AuthUserDto(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("firstName")] string FirstName,
        [property: JsonPropertyName("lastName")] string LastName,
        [property: JsonPropertyName("role")] string Role);
}
