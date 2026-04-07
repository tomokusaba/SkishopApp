using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin;

[Authorize]
public class LogoutModel(ILogger<LogoutModel> logger) : PageModel
{
    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        var userId = User.Identity?.Name ?? "unknown";
        await HttpContext.SignOutAsync("AdminCookies");
        logger.LogInformation("管理者ログアウト: UserId={UserId}", userId);
        return RedirectToPage("/Admin/Login");
    }
}
