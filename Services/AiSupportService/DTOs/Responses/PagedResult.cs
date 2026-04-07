namespace AiSupportService.DTOs.Responses;

/// <summary>
/// ページネーション付きの汎用レスポンス DTO。
/// </summary>
/// <typeparam name="T">ページ内アイテムの型。</typeparam>
/// <param name="Items">現在のページのアイテムリスト。</param>
/// <param name="TotalCount">全件数。</param>
/// <param name="Page">現在のページ番号（1 始まり）。</param>
/// <param name="PageSize">1 ページあたりの件数。</param>
/// <param name="TotalPages">総ページ数。</param>
public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
