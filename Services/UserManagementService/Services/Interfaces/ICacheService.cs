namespace UserManagementService.Services.Interfaces;

/// <summary>
/// Redis キャッシュサービス抽象。キャッシュ障害時はキャッシュミスとして処理する（サービスは正常継続）。
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
}
