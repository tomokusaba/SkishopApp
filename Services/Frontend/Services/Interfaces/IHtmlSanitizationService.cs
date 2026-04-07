using Microsoft.AspNetCore.Components;

namespace Frontend.Services.Interfaces;

/// <summary>
/// XSS 防止用 HTML サニタイザーインターフェース
/// §16.9 準拠 — AI チャットレスポンス等のリッチテキストをホワイトリスト方式で安全に表示
/// </summary>
public interface IHtmlSanitizationService
{
    /// <summary>
    /// 危険な HTML をサニタイズし、安全な MarkupString を返す
    /// </summary>
    MarkupString Sanitize(string dirtyHtml);
}
