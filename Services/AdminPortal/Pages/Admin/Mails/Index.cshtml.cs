using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Mails;

[Authorize(Policy = "AdminOnly")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string ActiveTab { get; set; } = "templates";

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 20;

    public List<MailTemplateItem> Templates { get; set; } = [];
    public List<MailLogItem> MailLogs { get; set; } = [];
    public MailStatsData? Stats { get; set; }
    public string? StatusMessage { get; set; }

    [BindProperty]
    public MailTemplateInput TemplateForm { get; set; } = new();

    [BindProperty]
    public TestSendInput TestSend { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        logger.LogInformation("メール管理ページにアクセス: Operator={Operator}, Tab={Tab}",
            User.Identity?.Name ?? "unknown", ActiveTab);

        switch (ActiveTab)
        {
            case "templates":
                await LoadTemplatesAsync(ct);
                break;
            case "logs":
                await LoadMailLogsAsync(ct);
                break;
            case "stats":
                await LoadStatsAsync(ct);
                break;
            case "test":
                await LoadTemplatesAsync(ct);
                break;
        }
    }

    public async Task<IActionResult> OnPostCreateTemplateAsync(CancellationToken ct = default)
    {
        logger.LogInformation("テンプレート作成リクエスト: Name={Name}, Operator={Operator}",
            TemplateForm.Name, User.Identity?.Name ?? "unknown");

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.PostAsJsonAsync("/admin/mail/templates", TemplateForm, ct);

            StatusMessage = response.IsSuccessStatusCode
                ? "テンプレートを作成しました。"
                : "テンプレートの作成に失敗しました。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "テンプレート作成中にエラー: Name={Name}", TemplateForm.Name);
            StatusMessage = "テンプレート作成中にエラーが発生しました。";
        }

        ActiveTab = "templates";
        await LoadTemplatesAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteTemplateAsync(string templateId, CancellationToken ct = default)
    {
        logger.LogInformation("テンプレート削除リクエスト: TemplateId={TemplateId}, Operator={Operator}",
            templateId, User.Identity?.Name ?? "unknown");

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.DeleteAsync($"/admin/mail/templates/{templateId}", ct);

            StatusMessage = response.IsSuccessStatusCode
                ? "テンプレートを削除しました。"
                : "テンプレートの削除に失敗しました。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "テンプレート削除中にエラー: TemplateId={TemplateId}", templateId);
            StatusMessage = "テンプレート削除中にエラーが発生しました。";
        }

        ActiveTab = "templates";
        await LoadTemplatesAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostTestSendAsync(CancellationToken ct = default)
    {
        logger.LogInformation("テスト送信リクエスト: TemplateId={TemplateId}, Operator={Operator}",
            TestSend.TemplateId, User.Identity?.Name ?? "unknown");

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.PostAsJsonAsync("/admin/mail/test", TestSend, ct);

            StatusMessage = response.IsSuccessStatusCode
                ? "テストメールを送信しました。"
                : "テストメールの送信に失敗しました。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "テスト送信中にエラー: TemplateId={TemplateId}", TestSend.TemplateId);
            StatusMessage = "テスト送信中にエラーが発生しました。";
        }

        ActiveTab = "test";
        await LoadTemplatesAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostResendAsync(string logId, CancellationToken ct = default)
    {
        logger.LogInformation("メール再送リクエスト: LogId={LogId}, Operator={Operator}",
            logId, User.Identity?.Name ?? "unknown");

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.PostAsync($"/admin/mail/logs/{logId}/retry", null, ct);

            StatusMessage = response.IsSuccessStatusCode
                ? "メールを再送しました。"
                : "メールの再送に失敗しました。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "メール再送中にエラー: LogId={LogId}", logId);
            StatusMessage = "メール再送中にエラーが発生しました。";
        }

        ActiveTab = "logs";
        await LoadMailLogsAsync(ct);
        return Page();
    }

    private async Task LoadTemplatesAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync("/admin/mail/templates", ct);
        if (response.IsSuccessStatusCode)
        {
            Templates = await response.Content.ReadFromJsonAsync<List<MailTemplateItem>>(cancellationToken: ct) ?? [];
        }
        else
        {
            logger.LogWarning("メールテンプレート取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    private async Task LoadMailLogsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var query = $"/admin/mail/logs?page={CurrentPage}&pageSize={PageSize}";
        var response = await client.GetAsync(query, ct);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<MailLogResponse>(cancellationToken: ct);
            if (result is not null)
            {
                MailLogs = result.Items;
                TotalPages = result.TotalPages;
            }
        }
        else
        {
            logger.LogWarning("メールログ取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync("/admin/mail/stats", ct);
        if (response.IsSuccessStatusCode)
        {
            Stats = await response.Content.ReadFromJsonAsync<MailStatsData>(cancellationToken: ct);
        }
        else
        {
            logger.LogWarning("メール統計取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    public record MailTemplateItem(
        string Id,
        string Name,
        string Subject,
        string BodyPreview,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public record MailLogItem(
        string Id,
        string TemplateName,
        string RecipientEmail,
        string Subject,
        string Status,
        DateTime SentAt,
        string? ErrorMessage);

    public record MailLogResponse(
        List<MailLogItem> Items,
        int TotalCount,
        int TotalPages,
        int CurrentPage);

    public record MailStatsData(
        decimal SuccessRate,
        decimal DeliveryRate,
        int TotalSent,
        int TotalFailed,
        int TotalBounced,
        List<DailyMailStat> DailyStats);

    public record DailyMailStat(DateTime Date, int Sent, int Delivered, int Failed);

    public class MailTemplateInput
    {
        [Required(ErrorMessage = "テンプレート名は必須です")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "件名は必須です")]
        [StringLength(500)]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "本文は必須です")]
        public string Body { get; set; } = string.Empty;
    }

    public class TestSendInput
    {
        [Required(ErrorMessage = "テンプレートIDは必須です")]
        public string TemplateId { get; set; } = string.Empty;

        [Required(ErrorMessage = "送信先メールアドレスは必須です")]
        [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
        [StringLength(255)]
        public string RecipientEmail { get; set; } = string.Empty;
    }
}
