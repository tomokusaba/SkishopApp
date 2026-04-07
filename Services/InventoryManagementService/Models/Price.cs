using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementService.Models;

/// <summary>
/// 商品価格エンティティ。prices テーブルにマッピングされる。
/// 独立 Aggregate Root として、通常価格・セール価格・適用期間を管理する。
/// 価格更新トランザクションと履歴管理のため、IPriceRepository 経由で操作する。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>セール価格は SaleStartDate 〜 SaleEndDate の期間内のみ有効。</item>
/// <item>通貨コードは ISO 4217 形式（デフォルト: JPY）。</item>
/// <item>楽観的ロック制御のため RowVersion（row_version）カラムを使用する。</item>
/// </list>
/// </remarks>
[Table("prices")]
public class Price
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("product_id")]
    [Required]
    [MaxLength(36)]
    public string ProductId { get; set; } = string.Empty;

    /// <summary>通常販売価格。</summary>
    [Column("regular_price")]
    public decimal RegularPrice { get; set; }

    /// <summary>セール価格。セール期間外は null。</summary>
    [Column("sale_price")]
    public decimal? SalePrice { get; set; }

    /// <summary>セール開始日時。</summary>
    [Column("sale_start_date")]
    public DateTimeOffset? SaleStartDate { get; set; }

    /// <summary>セール終了日時。</summary>
    [Column("sale_end_date")]
    public DateTimeOffset? SaleEndDate { get; set; }

    /// <summary>通貨コード（ISO 4217）。デフォルトは JPY。</summary>
    [Column("currency_code")]
    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "JPY";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("created_by")]
    [MaxLength(255)]
    public string? CreatedBy { get; set; }

    [Column("updated_by")]
    [MaxLength(255)]
    public string? UpdatedBy { get; set; }

    /// <summary>楽観的ロック用のタイムスタンプ。</summary>
    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// 通常価格を更新する。
    /// </summary>
    /// <param name="newPrice">新しい通常価格（0 以上）</param>
    /// <exception cref="ArgumentOutOfRangeException">価格が負数の場合</exception>
    public void UpdateRegularPrice(decimal newPrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(newPrice);
        RegularPrice = newPrice;
    }

    /// <summary>
    /// セール価格を適用する。
    /// </summary>
    /// <param name="salePrice">セール価格</param>
    /// <param name="startDate">セール開始日</param>
    /// <param name="endDate">セール終了日</param>
    /// <exception cref="ArgumentOutOfRangeException">セール価格が負数の場合</exception>
    /// <exception cref="ArgumentException">開始日が終了日より後の場合</exception>
    public void ApplySale(decimal salePrice, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(salePrice);
        if (startDate >= endDate)
            throw new ArgumentException("セール開始日は終了日より前である必要があります", nameof(startDate));

        SalePrice = salePrice;
        SaleStartDate = startDate;
        SaleEndDate = endDate;
    }

    /// <summary>
    /// セール設定を解除する。
    /// </summary>
    public void ClearSale()
    {
        SalePrice = null;
        SaleStartDate = null;
        SaleEndDate = null;
    }

    /// <summary>
    /// この価格設定を無効化する。
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// この価格設定を有効化する。
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// セール価格のみを更新する（開始・終了日は変更しない）。
    /// 既存のセール期間がある場合に、価格のみを部分更新するために使用。
    /// </summary>
    /// <param name="newSalePrice">新しいセール価格（null 許容）</param>
    public void UpdateSalePrice(decimal? newSalePrice)
    {
        if (newSalePrice.HasValue)
            ArgumentOutOfRangeException.ThrowIfNegative(newSalePrice.Value);
        SalePrice = newSalePrice;
    }

    /// <summary>
    /// セール開始日のみを更新する。
    /// </summary>
    /// <param name="newStartDate">新しいセール開始日</param>
    public void UpdateSaleStartDate(DateTimeOffset? newStartDate)
    {
        SaleStartDate = newStartDate;
    }

    /// <summary>
    /// セール終了日のみを更新する。
    /// </summary>
    /// <param name="newEndDate">新しいセール終了日</param>
    public void UpdateSaleEndDate(DateTimeOffset? newEndDate)
    {
        SaleEndDate = newEndDate;
    }

    /// <summary>親商品へのナビゲーション。</summary>
    public Product Product { get; set; } = null!;
}
