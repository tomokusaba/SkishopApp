namespace InventoryManagementService.Constants;

/// <summary>
/// OutboxEvent ステータス定数。
/// M-1〜M-5: マジックストリングの定数化。
/// </summary>
public static class OutboxStatus
{
    /// <summary>未発行（初期状態）</summary>
    public const string Pending = "PENDING";
    /// <summary>発行処理中</summary>
    public const string Processing = "PROCESSING";
    /// <summary>発行完了</summary>
    public const string Published = "PUBLISHED";
    /// <summary>発行失敗（リトライ可能）</summary>
    public const string Failed = "FAILED";
    /// <summary>デッドレター（リトライ上限到達）</summary>
    public const string DeadLetter = "DEAD_LETTER";
}

/// <summary>
/// Inventory ステータス定数。
/// </summary>
public static class InventoryStatus
{
    /// <summary>在庫あり</summary>
    public const string InStock = "IN_STOCK";
    /// <summary>在庫僅少</summary>
    public const string LowStock = "LOW_STOCK";
    /// <summary>在庫切れ</summary>
    public const string OutOfStock = "OUT_OF_STOCK";
    /// <summary>予約済み</summary>
    public const string Reserved = "RESERVED";
    /// <summary>販売終了</summary>
    public const string Discontinued = "DISCONTINUED";
}

/// <summary>
/// Review ステータス定数。
/// </summary>
public static class ReviewStatus
{
    /// <summary>審査待ち</summary>
    public const string Pending = "PENDING";
    /// <summary>承認済み</summary>
    public const string Approved = "APPROVED";
    /// <summary>却下</summary>
    public const string Rejected = "REJECTED";
}

/// <summary>
/// PriceHistory 価格種別定数。
/// </summary>
public static class PriceType
{
    /// <summary>通常価格</summary>
    public const string Regular = "REGULAR";
    /// <summary>セール価格</summary>
    public const string Sale = "SALE";
    /// <summary>プロモーション価格</summary>
    public const string Promotion = "PROMOTION";
}

/// <summary>
/// ProductImage 画像種別定数。
/// </summary>
public static class ImageType
{
    /// <summary>メイン画像</summary>
    public const string Main = "MAIN";
    /// <summary>ギャラリー画像</summary>
    public const string Gallery = "GALLERY";
    /// <summary>サムネイル画像</summary>
    public const string Thumbnail = "THUMBNAIL";
    /// <summary>詳細画像</summary>
    public const string Detail = "DETAIL";
}
