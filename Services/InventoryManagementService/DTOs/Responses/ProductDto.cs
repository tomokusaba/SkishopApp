namespace InventoryManagementService.DTOs.Responses;

/// <summary>
/// 商品一覧用レスポンス DTO。
/// GET /products エンドポイントのリスト表示で使用する。
/// 商品の基本情報のみを含む軽量な DTO。
/// </summary>
/// <param name="Id">商品 ID</param>
/// <param name="Sku">SKU コード</param>
/// <param name="Name">商品名</param>
/// <param name="Description">商品説明</param>
/// <param name="Brand">ブランド名</param>
/// <param name="CategoryName">所属カテゴリ名</param>
/// <param name="Weight">重量（kg 単位）</param>
/// <param name="IsActive">有効/無効フラグ</param>
/// <param name="CreatedAt">作成日時</param>
// M-11: sealed record 追加
public sealed record ProductDto(
    string Id,
    string Sku,
    string Name,
    string? Description,
    string? Brand,
    string? CategoryName,
    decimal? Weight,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>
/// 商品詳細レスポンス DTO。
/// GET /products/{id} エンドポイントで使用する。
/// カテゴリ・価格・在庫・画像・レビュー等の関連情報を含む詳細な DTO。
/// </summary>
/// <param name="Id">商品 ID</param>
/// <param name="Sku">SKU コード</param>
/// <param name="Name">商品名</param>
/// <param name="Description">商品説明</param>
/// <param name="Brand">ブランド名</param>
/// <param name="Attributes">商品属性（JSON 文字列）</param>
/// <param name="Tags">タグ一覧</param>
/// <param name="Weight">重量（kg 単位）</param>
/// <param name="IsActive">有効/無効フラグ</param>
/// <param name="Category">所属カテゴリ情報</param>
/// <param name="CurrentPrice">現在の価格情報</param>
/// <param name="Inventory">在庫情報</param>
/// <param name="Images">商品画像リスト</param>
/// <param name="CreatedAt">作成日時</param>
/// <param name="UpdatedAt">最終更新日時</param>
// M-11: sealed record 追加
public sealed record ProductDetailDto(
    string Id,
    string Sku,
    string Name,
    string? Description,
    string? Brand,
    string? Attributes,
    string[]? Tags,
    decimal? Weight,
    bool IsActive,
    CategoryDto? Category,
    PriceDto? CurrentPrice,
    InventoryDto? Inventory,
    List<ProductImageDto> Images,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
