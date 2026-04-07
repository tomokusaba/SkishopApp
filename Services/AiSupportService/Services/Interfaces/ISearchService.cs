using AiSupportService.DTOs.Requests;
using AiSupportService.DTOs.Responses;

namespace AiSupportService.Services.Interfaces;

/// <summary>
/// AI を活用した商品検索機能を提供するサービスインターフェース。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、InventoryManagementService の商品検索 API をラップし、
/// 検索分析、キャッシング、ユーザーフィードバック収集を統合します。
/// </para>
/// <para>
/// 機能:
/// <list type="bullet">
///   <item><description>キーワード検索: 商品名、説明、タグを対象とした全文検索</description></item>
///   <item><description>フィルタリング: カテゴリ、価格帯による絞り込み</description></item>
///   <item><description>検索サジェスト: 入力中のキーワードに対する候補提示</description></item>
///   <item><description>検索分析: クエリ、レスポンス時間、結果件数の記録</description></item>
/// </list>
/// </para>
/// <para>
/// キャッシュ戦略:
/// 同一の検索条件に対する結果は 5 分間キャッシュされます。
/// キャッシュキーにはクエリ、カテゴリ、価格帯、ページ情報が含まれます。
/// </para>
/// </remarks>
public interface ISearchService
{
    /// <summary>
    /// キーワードおよびフィルタ条件に基づいて商品を検索する。
    /// </summary>
    /// <param name="request">
    /// 検索リクエスト。クエリ文字列、カテゴリ、価格帯、ページング情報を含みます。
    /// </param>
    /// <param name="userId">
    /// 検索を実行するユーザーの ID。<c>null</c> の場合は匿名検索として記録されます。
    /// 検索分析データに使用されます。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 検索結果レスポンス。クエリ、総件数、ページネーションされた商品リストを含みます。
    /// 各商品には関連度スコア（0.0〜1.0）が付与されます。
    /// </returns>
    /// <remarks>
    /// <para>
    /// 検索実行時に以下のメトリクスが記録されます:
    /// <list type="bullet">
    ///   <item><description>検索リクエスト数</description></item>
    ///   <item><description>検索レスポンス時間</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 検索分析データ（クエリ、結果件数、レスポンス時間）はデータベースに保存され、
    /// 人気クエリ分析や検索品質の監視に使用されます。
    /// </para>
    /// </remarks>
    Task<SearchResultResponse> SearchAsync(SearchRequest request, string? userId = null, CancellationToken ct = default);

    /// <summary>
    /// 検索結果に対するユーザーフィードバックを記録する。
    /// </summary>
    /// <param name="request">
    /// フィードバックリクエスト。検索 ID とクリックされた商品 ID を含みます。
    /// </param>
    /// <param name="userId">
    /// フィードバックを提供するユーザーの ID。<c>null</c> の場合は匿名として記録されます。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// フィードバック記録操作を表すタスク。
    /// </returns>
    /// <remarks>
    /// <para>
    /// フィードバックデータは検索ランキングアルゴリズムの改善に使用されます。
    /// どの検索クエリでどの商品がクリックされたかを追跡することで、
    /// 検索結果の関連性を向上させることができます。
    /// </para>
    /// </remarks>
    Task RecordFeedbackAsync(SearchFeedbackRequest request, string? userId = null, CancellationToken ct = default);

    /// <summary>
    /// 入力クエリに基づくサジェスト候補を取得する。
    /// </summary>
    /// <param name="query">
    /// 入力中の検索クエリ文字列。部分一致で候補を検索します。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// サジェスト候補のリスト。最大 5 件の商品名が返されます。
    /// 候補がない場合は空のリストを返します。
    /// </returns>
    /// <remarks>
    /// <para>
    /// サジェスト機能は、ユーザーが検索ボックスに入力中に
    /// リアルタイムで候補を表示するために使用されます。
    /// </para>
    /// <para>
    /// 現在の実装では、商品名の前方一致検索を行い、
    /// 検索結果の上位 5 件の商品名を返します。
    /// </para>
    /// </remarks>
    Task<List<string>> GetSuggestionsAsync(string query, CancellationToken ct = default);
}
