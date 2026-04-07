namespace InventoryManagementService.DTOs.Responses;

/// <summary>
/// ページネーション付きリスト結果の汎用レスポンス DTO。
/// リスト系エンドポイントの共通レスポンス形式として使用する。
/// TotalPages・HasNext・HasPrevious を自動算出する。
/// </summary>
/// <typeparam name="T">リスト要素の型</typeparam>
/// <param name="Items">現在ページのデータリスト</param>
/// <param name="TotalElements">全件数</param>
/// <param name="Page">現在のページ番号（0 始まり）</param>
/// <param name="Size">1 ページあたりの件数</param>
public record PaginatedResult<T>(
    List<T> Items,
    long TotalElements,
    int Page,
    int Size)
{
    /// <summary>合計ページ数を算出する。</summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalElements / Size);

    /// <summary>次ページが存在するかを示す。</summary>
    public bool HasNext => Page < TotalPages - 1;

    /// <summary>前ページが存在するかを示す。</summary>
    public bool HasPrevious => Page > 0;
}
