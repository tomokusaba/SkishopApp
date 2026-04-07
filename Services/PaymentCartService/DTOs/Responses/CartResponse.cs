namespace PaymentCartService.DTOs.Responses;

/// <summary>
/// カート情報のレスポンス
/// </summary>
/// <param name="Id">カート ID</param>
/// <param name="CustomerId">顧客 ID（未ログインの場合は null）</param>
/// <param name="SessionId">セッション ID</param>
/// <param name="Status">カートステータス（Active, Expired, Abandoned, CheckedOut）</param>
/// <param name="Items">カート内のアイテム一覧</param>
/// <param name="TotalItems">アイテムの合計数量</param>
/// <param name="TotalAmount">合計金額</param>
/// <param name="ExpiresAt">カート有効期限</param>
/// <param name="CreatedAt">作成日時</param>
/// <param name="UpdatedAt">更新日時</param>
public record CartResponse(
    string Id,
    string? CustomerId,
    string SessionId,
    string Status,
    List<CartItemResponse> Items,
    int TotalItems,
    decimal TotalAmount,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
