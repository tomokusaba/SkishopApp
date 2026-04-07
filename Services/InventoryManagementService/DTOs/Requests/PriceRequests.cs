using System.ComponentModel.DataAnnotations;

namespace InventoryManagementService.DTOs.Requests;

/// <summary>
/// 価格新規作成リクエスト DTO。
/// POST /prices エンドポイントで使用する。
/// 通常価格・セール価格・通貨コードを設定する。
/// </summary>
/// <param name="ProductId">対象商品 ID（必須）</param>
/// <param name="RegularPrice">通常価格（0 以上）</param>
/// <param name="SalePrice">セール価格（任意、通常価格より低い値を指定）</param>
/// <param name="SaleStartDate">セール開始日時（任意）</param>
/// <param name="SaleEndDate">セール終了日時（任意、開始日時より後を指定）</param>
/// <param name="CurrencyCode">通貨コード（JPY/USD/EUR、デフォルト "JPY"）</param>
public record PriceCreateRequest(
    [Required]
    string ProductId,
    [Range(0, double.MaxValue, ErrorMessage = "通常価格は 0 以上を指定してください")]
    decimal RegularPrice,
    decimal? SalePrice,
    DateTimeOffset? SaleStartDate,
    DateTimeOffset? SaleEndDate,
    string CurrencyCode = "JPY");

/// <summary>
/// 価格更新リクエスト DTO。
/// PUT /prices/{id} エンドポイントで使用する。
/// 指定されたフィールドのみ部分更新を行う。
/// </summary>
/// <param name="RegularPrice">通常価格（任意、0 以上）</param>
/// <param name="SalePrice">セール価格（任意）</param>
/// <param name="SaleStartDate">セール開始日時（任意）</param>
/// <param name="SaleEndDate">セール終了日時（任意）</param>
/// <param name="IsActive">有効/無効フラグ（任意）</param>
public record PriceUpdateRequest(
    [Range(0, double.MaxValue)]
    decimal? RegularPrice,
    decimal? SalePrice,
    DateTimeOffset? SaleStartDate,
    DateTimeOffset? SaleEndDate,
    bool? IsActive);
