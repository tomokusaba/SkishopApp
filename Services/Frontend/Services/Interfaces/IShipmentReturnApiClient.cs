using Frontend.DTOs;

namespace Frontend.Services.Interfaces;

/// <summary>
/// 配送・返品 API クライアントインターフェース
/// BFF パターン: サーバーサイドから API Gateway 経由で販売管理サービスに通信
/// </summary>
public interface IShipmentReturnApiClient
{
    Task<ShipmentDto?> GetShipmentByOrderAsync(string orderId, CancellationToken ct = default);
    Task<ReturnDto?> CreateReturnRequestAsync(CreateReturnRequest request, CancellationToken ct = default);
    Task<List<ReturnDto>> GetReturnsByOrderAsync(string orderId, CancellationToken ct = default);
    Task<OrderDto?> GetOrderByNumberAndEmailAsync(string orderNumber, string email, CancellationToken ct = default);
}
