using System.ComponentModel;
using AiSupportService.Services.Interfaces;
using Microsoft.SemanticKernel;

namespace AiSupportService.Infrastructure.SemanticKernel.Plugins;

/// <summary>
/// Semantic Kernel の商品検索プラグイン。
/// </summary>
/// <remarks>
/// <para>
/// AI アシスタントが商品情報を検索・取得するために使用する Kernel Function を提供する。
/// Azure AI Search を活用したセマンティック検索により、自然言語でのクエリに対応する。
/// </para>
/// <para>
/// <b>提供機能:</b>
/// <list type="bullet">
///   <item><description><see cref="SearchProductsAsync"/>: キーワード・カテゴリ・価格帯での商品検索</description></item>
///   <item><description><see cref="GetProductDetailsAsync"/>: 商品 ID による詳細情報取得</description></item>
///   <item><description><see cref="GetCategoriesAsync"/>: 利用可能なカテゴリ一覧の取得</description></item>
/// </list>
/// </para>
/// <para>
/// <b>使用シナリオ:</b>
/// <list type="bullet">
///   <item><description>「初心者向けのスキー板を探して」→ キーワード検索</description></item>
///   <item><description>「3万円以下のブーツはある？」→ 価格帯での絞り込み検索</description></item>
///   <item><description>「この商品の詳細を教えて」→ 商品詳細の取得</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Kernel へのプラグイン登録
/// kernel.Plugins.AddFromObject(new ProductPlugin(productClient));
/// </code>
/// </example>
/// <param name="productClient">商品情報取得用のクライアント（InventoryManagementService/Azure AI Search 連携）。</param>
public class ProductPlugin(IProductClient productClient)
{
    /// <summary>
    /// キーワード・カテゴリ・価格帯を条件に商品を検索する。
    /// </summary>
    /// <param name="query">検索キーワード。自然言語でのクエリに対応。</param>
    /// <param name="category">カテゴリで絞り込む場合のカテゴリ名（省略可能）。</param>
    /// <param name="minPrice">最低価格での絞り込み（省略可能）。</param>
    /// <param name="maxPrice">最高価格での絞り込み（省略可能）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>検索結果の商品一覧を JSON 文字列で返す。</returns>
    /// <remarks>
    /// <para>
    /// AI モデルがユーザーの発話を分析し、適切なパラメータを設定してこの関数を呼び出す。
    /// 例: 「3万円以下の初心者向けスキー板」→ query="初心者向けスキー板", maxPrice=30000
    /// </para>
    /// <para>
    /// <b>レスポンス形式:</b>
    /// <code>
    /// [
    ///   { "ProductId": "SKI-001", "Name": "初心者向けスキー板", "Price": 25000, "Category": "スキー板" },
    ///   { "ProductId": "SKI-002", "Name": "オールラウンドスキー板", "Price": 28000, "Category": "スキー板" }
    /// ]
    /// </code>
    /// </para>
    /// </remarks>
    [KernelFunction("search_products")]
    [Description("商品を検索します。キーワード、カテゴリ、価格帯で絞り込みできます。")]
    public async Task<string> SearchProductsAsync(
        [Description("検索キーワード")] string query,
        [Description("カテゴリ（任意）")] string? category = null,
        [Description("最低価格（任意）")] decimal? minPrice = null,
        [Description("最高価格（任意）")] decimal? maxPrice = null,
        CancellationToken ct = default)
    {
        var products = await productClient.SearchProductsAsync(query, category, minPrice, maxPrice, ct);
        return System.Text.Json.JsonSerializer.Serialize(products);
    }

    /// <summary>
    /// 指定された商品 ID の詳細情報を取得する。
    /// </summary>
    /// <param name="productId">取得対象の商品 ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>
    /// 商品詳細の JSON 文字列。
    /// 商品が見つからない場合はエラーメッセージを返す。
    /// </returns>
    /// <remarks>
    /// <para>
    /// AI モデルは検索結果から特定の商品を選択した場合や、
    /// ユーザーが商品 ID を指定した場合にこの関数を呼び出す。
    /// </para>
    /// <para>
    /// <b>レスポンス形式:</b>
    /// <code>
    /// {
    ///   "ProductId": "SKI-001",
    ///   "Name": "初心者向けスキー板",
    ///   "Description": "初めてスキーをする方に最適な...",
    ///   "Price": 25000,
    ///   "Category": "スキー板",
    ///   "ImageUrl": "https://...",
    ///   "StockQuantity": 10,
    ///   "Specifications": { "Length": "160cm", "Level": "初心者" }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    [KernelFunction("get_product_details")]
    [Description("指定した商品IDの詳細情報を取得します。")]
    public async Task<string> GetProductDetailsAsync(
        [Description("商品ID")] string productId,
        CancellationToken ct = default)
    {
        var product = await productClient.GetProductByIdAsync(productId, ct);
        return product is not null
            ? System.Text.Json.JsonSerializer.Serialize(product)
            : "指定された商品が見つかりませんでした。";
    }

    /// <summary>
    /// 利用可能な商品カテゴリの一覧を取得する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>カテゴリ一覧の JSON 文字列（配列形式）。</returns>
    /// <remarks>
    /// <para>
    /// AI モデルは「どんな種類の商品がありますか」「カテゴリを教えて」といった
    /// ユーザー発話に対してこの関数を呼び出す。
    /// また、検索時のカテゴリ絞り込みのための情報提供にも使用される。
    /// </para>
    /// <para>
    /// <b>レスポンス形式:</b>
    /// <code>
    /// ["スキー板", "ブーツ", "ビンディング", "ウェア", "ゴーグル", "グローブ", "ヘルメット"]
    /// </code>
    /// </para>
    /// </remarks>
    [KernelFunction("get_product_categories")]
    [Description("利用可能な商品カテゴリの一覧を取得します。")]
    public async Task<string> GetCategoriesAsync(CancellationToken ct = default)
    {
        var categories = await productClient.GetCategoriesAsync(ct);
        return System.Text.Json.JsonSerializer.Serialize(categories);
    }
}
