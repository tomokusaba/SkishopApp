namespace InventoryManagementService.Exceptions;

/// <summary>
/// 在庫管理サービスで使用する構造化エラーコード定数。
/// Problem Details レスポンスの拡張プロパティとしてクライアントに返却され、
/// エラーの種類をプログラム的に判別可能にする。
/// </summary>
public static class ErrorCodes
{
    /// <summary>商品が見つからない。</summary>
    public const string ProductNotFound = "PROD_001";

    /// <summary>商品 SKU の重複。</summary>
    public const string ProductDuplicate = "PROD_002";

    /// <summary>無効化された商品への操作。</summary>
    public const string ProductInactive = "PROD_003";
    public const string InvalidOperation = "GEN_001";

    /// <summary>商品バリデーションエラー。</summary>
    public const string ProductValidation = "PROD_004";

    /// <summary>関連データが存在するため商品を削除できない。</summary>
    public const string ProductDeleteConflict = "PROD_005";

    /// <summary>在庫不足。</summary>
    public const string InsufficientStock = "INV_001";

    /// <summary>在庫予約が見つからない。</summary>
    public const string ReservationNotFound = "INV_002";

    /// <summary>在庫操作の競合（楽観的ロック等）。</summary>
    public const string InventoryConflict = "INV_003";

    /// <summary>価格情報が見つからない。</summary>
    public const string PriceNotFound = "PRICE_001";

    /// <summary>価格バリデーションエラー。</summary>
    public const string PriceValidation = "PRICE_002";

    /// <summary>メディアファイルのアップロード失敗。</summary>
    public const string MediaUploadFailed = "MEDIA_001";

    /// <summary>メディアファイルの形式が不正。</summary>
    public const string MediaInvalidFormat = "MEDIA_002";

    /// <summary>メディアファイルのサイズ上限超過。</summary>
    public const string MediaSizeExceeded = "MEDIA_003";
}
