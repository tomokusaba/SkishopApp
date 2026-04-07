namespace Frontend.Services.Interfaces;

/// <summary>
/// データ種別ごとのキャッシュ戦略を提供するキャッシュサービスインターフェース（§6.2）
/// </summary>
public interface ICacheService
{
    Task<T?> GetOrSetAsync<T>(
        string key,
        string dataType,
        Func<Task<T?>> factory,
        CancellationToken ct = default) where T : class;

    void Invalidate(string key);
    void InvalidateByPrefix(string prefix);
}
