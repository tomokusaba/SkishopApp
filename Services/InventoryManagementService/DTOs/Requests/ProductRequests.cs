using System.ComponentModel.DataAnnotations;

namespace InventoryManagementService.DTOs.Requests;

/// <summary>
/// 商品新規登録リクエスト DTO。
/// POST /products エンドポイントで使用する。
/// SKU は 'XX-YYYY-NNN' 形式（大文字英字・英数字・連番）で指定する。
/// </summary>
/// <param name="Sku">商品 SKU コード（必須、'XX-YYYY-NNN' 形式、最大 100 文字）</param>
/// <param name="Name">商品名（必須、最大 255 文字）</param>
/// <param name="Description">商品説明（任意、最大 5000 文字）</param>
/// <param name="Brand">ブランド名（任意、最大 100 文字）</param>
/// <param name="CategoryId">所属カテゴリ ID（必須）</param>
/// <param name="Attributes">商品属性（任意、JSON 文字列）</param>
/// <param name="Tags">タグ一覧（任意）</param>
/// <param name="Weight">重量（任意、kg 単位）</param>
public record ProductCreateRequest(
    [Required, StringLength(100), RegularExpression(@"^[A-Z]{2,5}-[A-Z0-9]+-\d{3,}$",
        ErrorMessage = "SKU は 'XX-YYYY-NNN' 形式で入力してください")]
    string Sku,
    [Required, StringLength(255)]
    string Name,
    [StringLength(5000)]
    string? Description,
    [StringLength(100)]
    string? Brand,
    [Required]
    string CategoryId,
    string? Attributes = null,
    string[]? Tags = null,
    decimal? Weight = null);

/// <summary>
/// 商品更新リクエスト DTO。
/// PUT /products/{id} エンドポイントで使用する。
/// 指定されたフィールドのみ部分更新を行う。
/// </summary>
/// <param name="Name">商品名（任意、最大 255 文字）</param>
/// <param name="Description">商品説明（任意、最大 5000 文字）</param>
/// <param name="Brand">ブランド名（任意、最大 100 文字）</param>
/// <param name="CategoryId">所属カテゴリ ID（任意）</param>
/// <param name="Attributes">商品属性（任意、JSON 文字列）</param>
/// <param name="Tags">タグ一覧（任意）</param>
/// <param name="Weight">重量（任意、kg 単位）</param>
/// <param name="IsActive">有効/無効フラグ（任意）</param>
public record ProductUpdateRequest(
    [StringLength(255)]
    string? Name,
    [StringLength(5000)]
    string? Description,
    [StringLength(100)]
    string? Brand,
    string? CategoryId,
    string? Attributes,
    string[]? Tags,
    decimal? Weight,
    bool? IsActive);

/// <summary>
/// 商品検索条件 DTO。
/// 内部サービス層で検索フィルタとして使用する。
/// </summary>
/// <param name="Keyword">検索キーワード（商品名・説明を対象に部分一致検索）</param>
/// <param name="CategoryId">カテゴリ ID でのフィルタ</param>
/// <param name="Brand">ブランド名でのフィルタ</param>
/// <param name="CategoryName">カテゴリ名でのフィルタ（CategoryId が未指定の場合に使用）</param>
public record ProductSearchCriteria(
    string? Keyword = null,
    string? CategoryId = null,
    string? Brand = null,
    string? CategoryName = null);

/// <summary>
/// 商品検索パラメータ DTO。
/// GET /products エンドポイントのクエリパラメータとして使用する。
/// 検索条件にページネーション・ソート設定を加えたもの。
/// </summary>
/// <param name="Keyword">検索キーワード（任意）</param>
/// <param name="CategoryId">カテゴリ ID でのフィルタ（任意）</param>
/// <param name="Category">カテゴリ名でのフィルタ（任意、CategoryId が未指定の場合に使用）</param>
/// <param name="Brand">ブランド名でのフィルタ（任意）</param>
/// <param name="Page">ページ番号（0 始まり、デフォルト 0）</param>
/// <param name="Size">1 ページあたりの件数（1〜100、デフォルト 20）</param>
/// <param name="SortBy">ソートフィールド（デフォルト "CreatedAt"）</param>
/// <param name="Descending">降順ソートフラグ（デフォルト true）</param>
public record ProductSearchParams(
    string? Keyword = null,
    string? CategoryId = null,
    string? Category = null,
    string? Brand = null,
    int Page = 0,
    int Size = 20,
    string SortBy = "CreatedAt",
    bool Descending = true);

/// <summary>
/// ページネーションパラメータ DTO。
/// リスト系エンドポイントで共通的に使用するページネーション設定。
/// </summary>
/// <param name="Page">ページ番号（0 始まり、デフォルト 0）</param>
/// <param name="Size">1 ページあたりの件数（1〜100、デフォルト 20）</param>
public record PaginationParams(
    int Page = 0,
    int Size = 20);
