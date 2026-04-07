namespace MailSendService.DTOs.Responses;

/// <summary>
/// ページネーション付きの汎用レスポンスラッパー。
/// </summary>
/// <typeparam name="T">結果アイテムの型。</typeparam>
/// <param name="Items">現在のページのアイテム一覧。</param>
/// <param name="Page">現在のページ番号（1 始まり）。</param>
/// <param name="PageSize">1 ページあたりのアイテム数。</param>
/// <param name="TotalCount">全体の総件数。</param>
/// <param name="TotalPages">全体のページ数。</param>
public record PaginatedResult<T>(
    List<T> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages);
