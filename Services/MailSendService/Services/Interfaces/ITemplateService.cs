using MailSendService.DTOs.Requests;
using MailSendService.DTOs.Responses;

namespace MailSendService.Services.Interfaces;

/// <summary>
/// テンプレートのレンダリング結果を表す DTO。件名・HTML 本文・プレーンテキスト本文を保持する。
/// </summary>
/// <param name="Subject">レンダリング済みのメール件名。</param>
/// <param name="HtmlBody">レンダリング済みの HTML 本文。</param>
/// <param name="PlainTextBody">レンダリング済みのプレーンテキスト本文（省略可）。</param>
public record RenderedMail(string Subject, string HtmlBody, string? PlainTextBody);

/// <summary>
/// メールテンプレートの CRUD 操作およびレンダリングを提供するサービスインターフェース。
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// 指定されたテンプレート名と変数を使用してメールをレンダリングする。
    /// </summary>
    /// <param name="templateName">レンダリングに使用するテンプレート名。</param>
    /// <param name="variables">テンプレートに埋め込む変数のディクショナリ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>レンダリング済みのメール（件名・本文）。</returns>
    /// <exception cref="NotFoundException">指定されたテンプレートが存在しない場合。</exception>
    Task<RenderedMail> RenderAsync(string templateName, Dictionary<string, object> variables, CancellationToken ct = default);

    /// <summary>
    /// 新しいメールテンプレートを作成する。
    /// </summary>
    /// <param name="request">テンプレート作成リクエスト。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>作成されたテンプレートのレスポンス。</returns>
    Task<MailTemplateResponse> CreateAsync(TemplateCreateRequest request, CancellationToken ct = default);

    /// <summary>
    /// 既存のメールテンプレートを更新する。
    /// </summary>
    /// <param name="id">更新対象のテンプレート ID。</param>
    /// <param name="request">テンプレート更新リクエスト。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>更新後のテンプレートのレスポンス。</returns>
    /// <exception cref="NotFoundException">指定されたテンプレートが存在しない場合。</exception>
    Task<MailTemplateResponse> UpdateAsync(string id, TemplateUpdateRequest request, CancellationToken ct = default);

    /// <summary>
    /// 指定された ID のメールテンプレートを取得する。
    /// </summary>
    /// <param name="id">テンプレートの一意識別子。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>テンプレートのレスポンス。</returns>
    /// <exception cref="NotFoundException">指定されたテンプレートが存在しない場合。</exception>
    Task<MailTemplateResponse> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// メールテンプレートの一覧をページネーション付きで取得する。
    /// </summary>
    /// <param name="page">取得するページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ページネーション付きテンプレートレスポンス。</returns>
    Task<PaginatedResult<MailTemplateResponse>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>
    /// 指定された ID のメールテンプレートを削除する。
    /// </summary>
    /// <param name="id">削除対象のテンプレート ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    /// <exception cref="NotFoundException">指定されたテンプレートが存在しない場合。</exception>
    Task DeleteAsync(string id, CancellationToken ct = default);
}
