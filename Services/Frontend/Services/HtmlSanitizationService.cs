using Frontend.Services.Interfaces;
using Ganss.Xss;

namespace Frontend.Services;

/// <summary>
/// XSS 防止用 HTML サニタイザー
/// §16.9 準拠 — AI チャットレスポンス等のリッチテキストをホワイトリスト方式で安全に表示
/// </summary>
public class HtmlSanitizationService : IHtmlSanitizationService
{
    private static readonly string[] AllowedTags = [
        "p", "br", "strong", "em", "ul", "ol", "li", "a", "code", "pre",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "blockquote", "hr", "span", "del",
        "table", "thead", "tbody", "tr", "td", "th"
    ];
    private static readonly string[] AllowedAttributes = ["href", "target", "rel", "class"];

    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizationService()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in AllowedTags)
        {
            _sanitizer.AllowedTags.Add(tag);
        }

        _sanitizer.AllowedAttributes.Clear();
        foreach (var attr in AllowedAttributes)
        {
            _sanitizer.AllowedAttributes.Add(attr);
        }

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("http");
    }

    /// <summary>
    /// 危険な HTML をサニタイズし、安全な MarkupString を返す
    /// </summary>
    public Microsoft.AspNetCore.Components.MarkupString Sanitize(string dirtyHtml)
    {
        if (string.IsNullOrWhiteSpace(dirtyHtml))
        {
            return new Microsoft.AspNetCore.Components.MarkupString(string.Empty);
        }

        var cleanHtml = _sanitizer.Sanitize(dirtyHtml);
        return new Microsoft.AspNetCore.Components.MarkupString(cleanHtml);
    }
}
