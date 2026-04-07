namespace PointService.Services.Interfaces;

/// <summary>ポイント有効期限管理サービス。</summary>
public interface IExpiryService
{
    /// <summary>有効期限切れポイントの一括失効処理（日次バッチ）</summary>
    Task<int> ProcessExpiredPointsAsync(CancellationToken ct = default);

    /// <summary>ユーザーの失効予定ポイント取得</summary>
    Task<List<Models.PointExpiry>> GetExpiringPointsAsync(
        string userId, int withinDays = 30, CancellationToken ct = default);
}
