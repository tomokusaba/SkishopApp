namespace PaymentCartService.DTOs.Responses;

/// <summary>
/// ページネーション付きのレスポンス
/// </summary>
/// <typeparam name="T">アイテムの型</typeparam>
/// <param name="Items">現在のページのアイテム一覧</param>
/// <param name="Page">現在のページ番号（1から開始）</param>
/// <param name="PageSize">1ページあたりのアイテム数</param>
/// <param name="TotalCount">全アイテム数</param>
/// <param name="TotalPages">総ページ数</param>
public record PaginatedResponse<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
