using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Services.Interfaces;

namespace InventoryManagementService.BackgroundServices;

/// <summary>
/// 起動時キャッシュウォームアップ BackgroundService。
/// アプリケーション起動時に頻繁にアクセスされるデータ（商品一覧、カテゴリ一覧）を
/// Redis キャッシュにプリロードする。
/// </summary>
/// <param name="scopeFactory">Scoped サービス取得用ファクトリー</param>
/// <param name="logger">ロガー</param>
/// <remarks>
/// - 起動時に 1 回のみ実行（ループなし）
/// - ウォームアップ失敗時はアプリケーション起動を継続する（Warning ログのみ）
/// - 商品の先頭 100 件とカテゴリ全件をキャッシュに格納
/// </remarks>
public class CacheWarmupService(
    IServiceScopeFactory scopeFactory,
    ILogger<CacheWarmupService> logger) : BackgroundService
{
    /// <summary>
    /// キャッシュウォームアップ処理を実行する。
    /// </summary>
    /// <param name="stoppingToken">停止トークン</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CacheWarmupService started — 起動時キャッシュウォームアップ開始");

        try
        {
            using var scope = scopeFactory.CreateScope();
            var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
            var categoryService = scope.ServiceProvider.GetRequiredService<ICategoryService>();

            await productService.SearchAsync(new ProductSearchCriteria(), 0, 100, stoppingToken);
            await categoryService.GetAllAsync(stoppingToken);

            logger.LogInformation("CacheWarmupService 完了: キャッシュウォームアップ成功");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "CacheWarmupService: ウォームアップ失敗（起動を継続）: {Message}", ex.Message);
        }
    }
}
