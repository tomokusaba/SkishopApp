namespace Frontend.Models;

/// <summary>
/// RFC 9457 (Problem Details for HTTP APIs) 準拠のエラーレスポンス型定義。
/// バックエンド ASP.NET Core の TypedResults.Problem() が返すレスポンスに対応。
/// §16.1 準拠 — 7 フィールド。
/// </summary>
public record ProblemDetailsResponse(
    string? Type,
    string Title,
    int Status,
    string? Detail = null,
    string? Instance = null,
    Dictionary<string, string[]>? Errors = null,
    string? TraceId = null);

/// <summary>
/// エラー表示方法の分類（Blazor コンポーネント向け）
/// §16.1 準拠
/// </summary>
public enum ErrorDisplayType
{
    Inline,
    Snackbar,
    Page,
    Dialog
}
