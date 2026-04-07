using Fluxor;

namespace Frontend.Store.CartStore;

/// <summary>
/// カートの Fluxor State
/// §6 準拠 — グローバル状態管理
/// H-27: ServerTotalAmount を優先し、フロントエンド計算はフォールバックとして保持
/// </summary>
[FeatureState]
public record CartState
{
    public List<CartItem> Items { get; init; } = [];
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }
    public decimal? ServerTotalAmount { get; init; }
    public int TotalItemCount => Items.Sum(i => i.Quantity);
    public decimal TotalAmount => ServerTotalAmount ?? Items.Sum(i => i.Price * i.Quantity);
}

/// <summary>
/// カートアイテム
/// </summary>
public record CartItem(
    string ProductId,
    string ProductName,
    decimal Price,
    int Quantity,
    string? ImageUrl = null,
    int StockQuantity = 0);
