namespace InventoryManagementService.Configurations;

/// <summary>
/// Redis キャッシュの TTL（有効期間）設定。appsettings.json の "Cache" セクションにバインドされる。
/// リソース種別ごとにキャッシュの有効期間を秒単位で指定する。
/// </summary>
public record CacheConfig
{
    /// <summary>キャッシュの有効・無効フラグ。false の場合、全キャッシュ操作がバイパスされる。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>デフォルトのキャッシュ TTL（秒）。個別 TTL が未設定のリソースに適用される。</summary>
    public int DefaultTtlSeconds { get; init; } = 600;

    /// <summary>商品データのキャッシュ TTL（秒）。更新頻度が低いため長めに設定。</summary>
    public int ProductTtlSeconds { get; init; } = 1800;

    /// <summary>カテゴリデータのキャッシュ TTL（秒）。マスタデータのため最も長く設定。</summary>
    public int CategoryTtlSeconds { get; init; } = 3600;

    /// <summary>在庫データのキャッシュ TTL（秒）。変動が激しいため短めに設定。</summary>
    public int InventoryTtlSeconds { get; init; } = 300;

    /// <summary>価格データのキャッシュ TTL（秒）。</summary>
    public int PriceTtlSeconds { get; init; } = 900;

    /// <summary>検索結果のキャッシュ TTL（秒）。</summary>
    public int SearchTtlSeconds { get; init; } = 600;
}
