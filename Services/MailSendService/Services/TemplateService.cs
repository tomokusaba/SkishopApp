using System.Text.Json;
using System.Text.RegularExpressions;
using Ganss.Xss;
using MailSendService.DTOs.Requests;
using MailSendService.DTOs.Responses;
using MailSendService.Exceptions;
using MailSendService.Models;
using MailSendService.Repositories.Interfaces;
using MailSendService.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace MailSendService.Services;

/// <summary>
/// メールテンプレートの管理と変数展開を担うサービス。
/// Redis キャッシュによる高速なテンプレート取得と、DB フォールバックによる信頼性を両立する。
/// </summary>
/// <remarks>
/// <para>テンプレートは Redis に 5 分間（<see cref="CacheTtlMinutes"/>）キャッシュされる。</para>
/// <para>テンプレート更新・削除時にはキャッシュを明示的に無効化する。</para>
/// <para>削除は論理削除（IsActive = false）で実行される。</para>
/// <para>P1-10: HtmlBody は保存前に HtmlSanitizer で XSS サニタイズを行う。</para>
/// </remarks>
public partial class TemplateService(
    IMailTemplateRepository templateRepository,
    IDistributedCache cache,
    ILogger<TemplateService> logger) : ITemplateService
{
    private const int CacheTtlMinutes = 5;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// P1-10: HtmlSanitizer インスタンス（スレッドセーフ）。
    /// 許可タグ: メール送信に安全な基本的 HTML タグのみ許可。
    /// </summary>
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        // メール用に安全なタグのみ許可（script, iframe 等は自動でブロック）
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "strong", "b", "em", "i", "u", "a", "ul", "ol", "li",
            "h1", "h2", "h3", "h4", "h5", "h6", "table", "tr", "td", "th", "thead", "tbody",
            "span", "div", "img", "blockquote", "hr" })
        {
            sanitizer.AllowedTags.Add(tag);
        }
        // 安全な属性のみ許可
        sanitizer.AllowedAttributes.Clear();
        foreach (var attr in new[] { "href", "src", "alt", "title", "style", "class", "id", "width", "height" })
        {
            sanitizer.AllowedAttributes.Add(attr);
        }
        // href/src は http/https/mailto のみ許可
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");
        return sanitizer;
    }

    /// <summary>
    /// HtmlBody を XSS サニタイズする（P1-10）。
    /// </summary>
    private static string? SanitizeHtmlBody(string? htmlBody)
        => htmlBody is null ? null : Sanitizer.Sanitize(htmlBody);

    /// <summary>
    /// 指定されたテンプレート名でテンプレートを取得し、変数を展開してレンダリング済みメールを返す。
    /// </summary>
    /// <param name="templateName">テンプレート名。</param>
    /// <param name="variables">テンプレート変数のキー・値ペア。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>件名・HTML 本文・テキスト本文を含むレンダリング済みメール。</returns>
    /// <remarks>
    /// <para>取得順序: Redis キャッシュ → DB フォールバック。キャッシュミス時は DB から取得し Redis に 5 分間キャッシュする。</para>
    /// <para>HtmlBody の変数展開時は XSS 防止のため HTML エンコードを適用する。Subject・TextBody はエンコードなし。</para>
    /// <para>変数プレースホルダーは <c>{{variableName}}</c> 形式。</para>
    /// </remarks>
    /// <exception cref="TemplateNotFoundException">指定されたテンプレート名が DB に存在しない場合。</exception>
    /// <exception cref="TemplateRenderException">テンプレート変数の展開中にエラーが発生した場合。</exception>
    public async Task<RenderedMail> RenderAsync(string templateName, Dictionary<string, object> variables, CancellationToken ct = default)
    {
        var cacheKey = $"mail:template:{templateName}";
        var cached = await cache.GetStringAsync(cacheKey, ct);
        MailTemplate? template;

        if (cached is not null)
        {
            template = JsonSerializer.Deserialize<MailTemplate>(cached, JsonOptions);
            if (template is null)
            {
                // Cache corruption — fall back to DB
                template = await templateRepository.FindActiveByNameAsync(templateName, ct)
                    ?? throw new TemplateNotFoundException(templateName);
            }
        }
        else
        {
            template = await templateRepository.FindActiveByNameAsync(templateName, ct)
                ?? throw new TemplateNotFoundException(templateName);

            await cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(template, JsonOptions),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheTtlMinutes)
                }, ct);
        }

        try
        {
            var subject = ReplaceVariables(template.Subject, variables, htmlEncode: false);
            var htmlBody = template.HtmlBody is not null ? ReplaceVariables(template.HtmlBody, variables, htmlEncode: true) : string.Empty;
            var textBody = template.TextBody is not null ? ReplaceVariables(template.TextBody, variables, htmlEncode: false) : null;

            return new RenderedMail(subject, htmlBody, textBody);
        }
        catch (Exception ex) when (ex is not TemplateNotFoundException)
        {
            logger.LogError(ex, "Template rendering failed: {TemplateName}", templateName);
            throw new TemplateRenderException(templateName, ex);
        }
    }

    /// <summary>
    /// 新しいメールテンプレートを作成する。
    /// </summary>
    /// <param name="request">テンプレート作成リクエスト（名前、件名、本文、変数定義等）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>作成されたテンプレートのレスポンス。</returns>
    /// <exception cref="DuplicateTemplateNameException">同名のテンプレートが既に存在する場合。</exception>
    /// <remarks>P1-10: HtmlBody は保存前に XSS サニタイズを適用する。</remarks>
    public async Task<MailTemplateResponse> CreateAsync(TemplateCreateRequest request, CancellationToken ct = default)
    {
        if (await templateRepository.ExistsByNameAsync(request.Name, ct))
            throw new DuplicateTemplateNameException(request.Name);

        var template = new MailTemplate
        {
            Name = request.Name,
            Subject = request.Subject,
            TemplateType = request.TemplateType,
            Variables = request.Variables
        };
        // DDD: 本文を更新（XSS サニタイズ済み）
        template.UpdateBody(SanitizeHtmlBody(request.HtmlBody), request.TextBody);

        await templateRepository.AddAsync(template, ct);
        await templateRepository.SaveChangesAsync(ct);

        logger.LogInformation("Template created: {TemplateName}", template.Name);
        return ToResponse(template);
    }

    /// <summary>
    /// 既存のメールテンプレートを部分更新する。更新後に Redis キャッシュを無効化する。
    /// </summary>
    /// <param name="id">更新対象のテンプレート ID。</param>
    /// <param name="request">更新内容（null のフィールドは更新しない）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>更新後のテンプレートレスポンス。</returns>
    /// <exception cref="TemplateNotFoundException">指定された ID のテンプレートが存在しない場合。</exception>
    /// <remarks>P1-10: HtmlBody は保存前に XSS サニタイズを適用する。</remarks>
    public async Task<MailTemplateResponse> UpdateAsync(string id, TemplateUpdateRequest request, CancellationToken ct = default)
    {
        var template = await templateRepository.FindByIdAsync(id, ct)
            ?? throw new TemplateNotFoundException(id);

        // DDD: 件名の更新
        if (request.Subject is not null) template.UpdateSubject(request.Subject);

        // DDD: 本文の更新（部分更新のため、現在の値と新しい値をマージ）
        if (request.HtmlBody is not null || request.TextBody is not null)
        {
            var newHtmlBody = request.HtmlBody is not null ? SanitizeHtmlBody(request.HtmlBody) : template.HtmlBody;
            var newTextBody = request.TextBody ?? template.TextBody;
            template.UpdateBody(newHtmlBody, newTextBody);
        }

        if (request.Variables is not null) template.Variables = request.Variables;

        // DDD: 有効/無効の切り替え
        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value)
                template.Activate();
            else
                template.Deactivate();
        }

        await templateRepository.SaveChangesAsync(ct);
        await cache.RemoveAsync($"mail:template:{template.Name}", ct);
        logger.LogInformation("Template updated: {TemplateId}", id);
        return ToResponse(template);
    }

    /// <summary>
    /// 指定された ID のメールテンプレートを取得する。
    /// </summary>
    /// <param name="id">テンプレート ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>テンプレートレスポンス。</returns>
    /// <exception cref="TemplateNotFoundException">指定された ID のテンプレートが存在しない場合。</exception>
    public async Task<MailTemplateResponse> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var template = await templateRepository.FindByIdAsync(id, ct)
            ?? throw new TemplateNotFoundException(id);
        return ToResponse(template);
    }

    /// <summary>
    /// 全テンプレートをページネーション付きで取得する。
    /// </summary>
    /// <param name="page">ページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ページネーション付きテンプレートレスポンス。</returns>
    public async Task<PaginatedResult<MailTemplateResponse>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (templates, totalCount) = await templateRepository.FindAllPagedAsync(page, pageSize, ct);
        var items = templates.Select(ToResponse).ToList();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        return new PaginatedResult<MailTemplateResponse>(items, page, pageSize, totalCount, totalPages);
    }

    /// <summary>
    /// メールテンプレートを論理削除する（IsActive = false に設定）。削除後に Redis キャッシュを無効化する。
    /// </summary>
    /// <param name="id">削除対象のテンプレート ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    /// <exception cref="TemplateNotFoundException">指定された ID のテンプレートが存在しない場合。</exception>
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var template = await templateRepository.FindByIdAsync(id, ct)
            ?? throw new TemplateNotFoundException(id);

        // DDD: テンプレートを無効化（論理削除）
        template.Deactivate();
        await templateRepository.SaveChangesAsync(ct);
        await cache.RemoveAsync($"mail:template:{template.Name}", ct);
        logger.LogInformation("Template deactivated: {TemplateId}", id);
    }

    /// <summary>
    /// テンプレート文字列内の <c>{{key}}</c> プレースホルダーを対応する変数値に置換する。
    /// </summary>
    /// <remarks>
    /// htmlEncode が true の場合、XSS 防止のため置換値を <see cref="System.Net.WebUtility.HtmlEncode"/> でエンコードする。
    /// 変数辞書に存在しないキーのプレースホルダーはそのまま残す。
    /// </remarks>
    private static string ReplaceVariables(string template, Dictionary<string, object> variables, bool htmlEncode)
    {
        return TemplateVariableRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (!variables.TryGetValue(key, out var value))
                return match.Value;
            var text = value?.ToString() ?? string.Empty;
            return htmlEncode ? System.Net.WebUtility.HtmlEncode(text) : text;
        });
    }

    /// <summary>
    /// MailTemplate エンティティを MailTemplateResponse DTO に変換する。
    /// </summary>
    private static MailTemplateResponse ToResponse(MailTemplate template)
        => new(template.Id, template.Name, template.Subject, template.HtmlBody,
            template.TextBody, template.TemplateType, template.Variables,
            template.IsActive, template.CreatedAt, template.UpdatedAt);

    /// <summary>
    /// テンプレート変数プレースホルダー <c>{{variableName}}</c> にマッチするコンパイル済み正規表現。
    /// </summary>
    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex TemplateVariableRegex();
}
