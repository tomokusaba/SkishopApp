namespace InventoryManagementService.DTOs.Responses;

/// <summary>
/// 価格情報レスポンス DTO。
/// 価格 API のレスポンスとして返却される。
/// 通常価格・セール価格・セール期間・現在セール中かどうかの情報を含む。
/// </summary>
/// <param name="Id">価格レコード ID</param>
/// <param name="ProductId">商品 ID</param>
/// <param name="RegularPrice">通常価格</param>
/// <param name="SalePrice">セール価格（セールなしの場合は null）</param>
/// <param name="SaleStartDate">セール開始日時</param>
/// <param name="SaleEndDate">セール終了日時</param>
/// <param name="CurrencyCode">通貨コード（JPY/USD/EUR）</param>
/// <param name="IsActive">有効/無効フラグ</param>
/// <param name="OnSale">現在セール中かどうか</param>
// M-10: sealed record 追加
public sealed record PriceDto(
    string Id,
    string ProductId,
    decimal RegularPrice,
    decimal? SalePrice,
    DateTimeOffset? SaleStartDate,
    DateTimeOffset? SaleEndDate,
    string CurrencyCode,
    bool IsActive,
    bool OnSale);

/// <summary>
/// 価格変更履歴レスポンス DTO。
/// 価格変更の監査証跡として使用される。
/// 変更前後の価格・変更理由・変更者の情報を含む。
/// </summary>
/// <param name="Id">履歴レコード ID</param>
/// <param name="ProductId">商品 ID</param>
/// <param name="Price">変更後の価格</param>
/// <param name="PriceType">価格種別（REGULAR/SALE）</param>
/// <param name="EffectiveDate">適用開始日時</param>
/// <param name="Reason">変更理由</param>
/// <param name="CurrencyCode">通貨コード</param>
/// <param name="ChangedBy">変更者の識別子</param>
/// <param name="CreatedAt">レコード作成日時</param>
// M-10: sealed record 追加
public sealed record PriceHistoryDto(
    string Id,
    string ProductId,
    decimal Price,
    string PriceType,
    DateTimeOffset EffectiveDate,
    string? Reason,
    string CurrencyCode,
    string? ChangedBy,
    DateTimeOffset CreatedAt);
