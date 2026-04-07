using AiSupportService.DTOs.Requests;
using AiSupportService.DTOs.Responses;

namespace AiSupportService.Services.Interfaces;

/// <summary>
/// AI による需要予測機能を提供するサービスインターフェース。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、過去の販売データに基づいて将来の商品需要を予測します。
/// 在庫管理、発注計画、マーケティング戦略の立案に使用されることを想定しています。
/// </para>
/// <para>
/// 予測アルゴリズム:
/// <list type="bullet">
///   <item><description>移動平均法（Moving Average）を使用して需要を予測します</description></item>
///   <item><description>過去 3 件以上のデータがある場合は履歴ベースで予測</description></item>
///   <item><description>データが不足している場合はデフォルト値を使用</description></item>
/// </list>
/// </para>
/// <para>
/// 信頼度スコア:
/// 変動係数（CV: Coefficient of Variation）に基づいて計算され、
/// 0.5（低信頼度）から 0.95（高信頼度）の範囲で出力されます。
/// 履歴データのばらつきが大きいほど、信頼度は低くなります。
/// </para>
/// </remarks>
public interface IForecastService
{
    /// <summary>
    /// 指定商品の需要予測を生成する。
    /// </summary>
    /// <param name="request">
    /// 予測生成リクエスト。商品 ID、SKU、予測期間（WEEKLY, MONTHLY, QUARTERLY, YEARLY）を含みます。
    /// </param>
    /// <param name="adminUserId">
    /// 予測を実行する管理者のユーザー ID。監査ログ用に記録されます。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 生成された需要予測レスポンス。予測需要数、信頼度スコア、モデルバージョンを含みます。
    /// </returns>
    /// <remarks>
    /// <para>
    /// 予測期間ごとのデフォルト需要（履歴データ不足時）:
    /// <list type="bullet">
    ///   <item><description>WEEKLY: 50</description></item>
    ///   <item><description>MONTHLY: 200</description></item>
    ///   <item><description>QUARTERLY: 600</description></item>
    ///   <item><description>YEARLY: 2400</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 生成された予測はデータベースに保存され、メトリクスとしても記録されます。
    /// </para>
    /// </remarks>
    Task<ForecastResponse> GenerateAsync(GenerateForecastRequest request, string adminUserId, CancellationToken ct = default);

    /// <summary>
    /// 指定商品の需要予測履歴を取得する。
    /// </summary>
    /// <param name="productId">
    /// 予測履歴を取得する商品の ID。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 予測レスポンスのリスト。該当する予測がない場合は空のリストを返します。
    /// </returns>
    /// <remarks>
    /// 結果は予測日時の降順（新しい順）でソートされます。
    /// </remarks>
    Task<List<ForecastResponse>> GetByProductIdAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 全ての需要予測データを取得する。
    /// </summary>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 全ての予測レスポンスのリスト。予測が存在しない場合は空のリストを返します。
    /// </returns>
    /// <remarks>
    /// <para>
    /// このメソッドは管理者向けの概要表示に使用されることを想定しています。
    /// データ量が多い場合は、ページネーション付きのメソッドの追加を検討してください。
    /// </para>
    /// <para>
    /// 結果は予測日時の降順（新しい順）でソートされます。
    /// </para>
    /// </remarks>
    Task<List<ForecastResponse>> GetAllAsync(CancellationToken ct = default);
}
