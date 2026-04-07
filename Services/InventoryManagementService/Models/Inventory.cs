using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventoryManagementService.Exceptions;

namespace InventoryManagementService.Models;

/// <summary>
/// 商品在庫エンティティ。inventories テーブルにマッピングされる。
/// 独立 Aggregate Root として、ロケーション別の在庫数量・予約数量・ステータスを管理する。
/// SELECT FOR UPDATE を伴うトランザクション制御のため、ProductRepository 経由ではなく
/// IInventoryRepository 経由で操作する。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>在庫操作は Reserve / Release / StockIn / StockOut のドメインメソッドで実行し、直接のプロパティ変更は避ける。</item>
/// <item>Status は DetermineStatus() により自動算出される（IN_STOCK / OUT_OF_STOCK / LOW_STOCK / RESERVED / DISCONTINUED）。</item>
/// <item>楽観的ロック制御のため RowVersion（row_version）カラムを使用する。</item>
/// </list>
/// </remarks>
[Table("inventories")]
public class Inventory
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("product_id")]
    [Required]
    [MaxLength(36)]
    public string ProductId { get; set; } = string.Empty;

    /// <summary>物理在庫数量（予約済み分を含む総数）。</summary>
    [Column("quantity")]
    public int Quantity { get; set; }

    /// <summary>注文によって予約されている数量。利用可能数 = Quantity - ReservedQuantity。</summary>
    [Column("reserved_quantity")]
    public int ReservedQuantity { get; set; }

    /// <summary>倉庫・棚番などのロケーション識別コード。</summary>
    [Column("location_code")]
    [Required]
    [MaxLength(20)]
    public string LocationCode { get; set; } = string.Empty;

    /// <summary>在庫ステータス。IN_STOCK / OUT_OF_STOCK / LOW_STOCK / RESERVED / DISCONTINUED のいずれか。</summary>
    [Column("status")]
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "IN_STOCK";

    /// <summary>発注点。在庫数がこの値以下になると LOW_STOCK に遷移する。</summary>
    [Column("reorder_point")]
    public int ReorderPoint { get; set; }

    /// <summary>最後に在庫が予約された日時。予約がすべて解放されると null になる。</summary>
    [Column("reserved_at")]
    public DateTimeOffset? ReservedAt { get; set; }

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

    /// <summary>楽観的ロック用のタイムスタンプ。EF Core が自動管理する。</summary>
    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>親商品へのナビゲーション。</summary>
    public Product Product { get; set; } = null!;

    /// <summary>
    /// 指定数量を在庫予約する。注文確定フロー（Saga）の在庫引当ステップで使用する。
    /// 利用可能数（Quantity - ReservedQuantity）が不足する場合は例外をスローする。
    /// </summary>
    /// <param name="quantity">予約する数量（正の整数）。</param>
    /// <exception cref="InsufficientStockException">利用可能数が不足している場合にスローされる。</exception>
    public void Reserve(int quantity)
    {
        var available = Quantity - ReservedQuantity;
        if (available < quantity)
            throw new InsufficientStockException(ProductId, quantity, available);

        ReservedQuantity += quantity;
        ReservedAt = DateTimeOffset.UtcNow;
        Status = DetermineStatus();
    }

    /// <summary>
    /// 予約済み在庫を解放する。注文キャンセル時や Saga の補償トランザクションで使用する。
    /// 指定数量が現在の予約数を超える場合は 0 に切り下げる（べき等性を保証）。
    /// </summary>
    /// <param name="quantity">解放する数量。</param>
    public void Release(int quantity)
    {
        ReservedQuantity = Math.Max(0, ReservedQuantity - quantity);
        if (ReservedQuantity == 0)
            ReservedAt = null;
        Status = DetermineStatus();
    }

    /// <summary>
    /// 入庫処理。仕入れ・返品受入などで物理在庫を増加させる。
    /// </summary>
    /// <param name="quantity">入庫数量（1 以上の正の整数）。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quantity"/> が 0 以下の場合にスローされる。</exception>
    public void StockIn(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        Quantity += quantity;
        Status = DetermineStatus();
    }

    /// <summary>
    /// 出庫処理。出荷確定時に物理在庫を減少させる。
    /// 利用可能数（Quantity - ReservedQuantity）が不足する場合は例外をスローする。
    /// </summary>
    /// <param name="quantity">出庫数量（1 以上の正の整数）。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quantity"/> が 0 以下の場合にスローされる。</exception>
    /// <exception cref="InsufficientStockException">利用可能数が不足している場合にスローされる。</exception>
    public void StockOut(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        var available = Quantity - ReservedQuantity;
        if (available < quantity)
            throw new InsufficientStockException(ProductId, quantity, available);

        Quantity -= quantity;
        Status = DetermineStatus();
    }

    /// <summary>
    /// 現在の数量・予約状況に基づいて在庫ステータスを算出する。
    /// DISCONTINUED は外部から明示的に設定された場合のみ維持される。
    /// </summary>
    /// <returns>
    /// 算出されたステータス文字列:
    /// DISCONTINUED（廃番）/ OUT_OF_STOCK（在庫切れ）/ RESERVED（予約あり）/ LOW_STOCK（発注点以下）/ IN_STOCK（在庫あり）。
    /// </returns>
    public string DetermineStatus()
    {
        if (Status == "DISCONTINUED")
            return "DISCONTINUED";

        return (Quantity, ReservedQuantity) switch
        {
            (0, _) => "OUT_OF_STOCK",
            (_, > 0) => "RESERVED",
            _ when Quantity <= ReorderPoint => "LOW_STOCK",
            _ => "IN_STOCK"
        };
    }
}
