namespace Frontend.Models;

/// <summary>
/// バックエンド ASP.NET Core のページネーションレスポンスに対応する汎用型。
/// 全サービス（Products, Orders, Points, Coupons, Reviews 等）で統一使用。
/// §16.4 準拠。
/// </summary>
public record PaginatedResult<T>(
    List<T> Items,
    long TotalElements,
    int Page,
    int Size)
{
    public int TotalPages => Size > 0 ? (int)Math.Ceiling((double)TotalElements / Size) : 0;
    public bool HasNext => Page < TotalPages - 1;
    public bool HasPrevious => Page > 0;
}

/// <summary>
/// ページネーションクエリパラメータ型
/// </summary>
public record PaginationParams(
    int Page = 0,
    int Size = 20,
    string? Sort = null);
