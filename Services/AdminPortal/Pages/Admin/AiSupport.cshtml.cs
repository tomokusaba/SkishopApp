using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin;

[Authorize(Roles = "ADMIN,MANAGER,STAFF")]
public class AiSupportModel(ILogger<AiSupportModel> logger) : PageModel
{
    public void OnGet()
    {
        logger.LogInformation("AIサポートページにアクセス: Operator={Operator}",
            User.Identity?.Name ?? "unknown");
    }
}
