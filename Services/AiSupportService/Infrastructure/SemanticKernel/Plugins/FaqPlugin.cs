using System.ComponentModel;
using AiSupportService.Repositories.Interfaces;
using Microsoft.SemanticKernel;

namespace AiSupportService.Infrastructure.SemanticKernel.Plugins;

/// <summary>
/// Semantic Kernel の FAQ プラグイン。
/// </summary>
/// <remarks>
/// <para>
/// AI アシスタントが返品・配送・支払い等のよくある質問（FAQ）に回答するための Kernel Function を提供する。
/// FAQ データはデータベースに格納されており、キーワード検索で該当する質問と回答を取得する。
/// </para>
/// <para>
/// <b>Semantic Kernel 統合:</b>
/// <see cref="KernelFunctionAttribute"/> で装飾されたメソッドは、AI モデルが自動的に呼び出すことができる。
/// <see cref="DescriptionAttribute"/> による説明文は、AI モデルが適切な関数を選択するために使用される。
/// </para>
/// <para>
/// <b>使用シナリオ:</b>
/// <list type="bullet">
///   <item><description>「返品方法を教えてください」→ 返品関連の FAQ を検索</description></item>
///   <item><description>「送料はいくらですか」→ 配送関連の FAQ を検索</description></item>
///   <item><description>「支払い方法は何がありますか」→ 決済関連の FAQ を検索</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Kernel へのプラグイン登録
/// kernel.Plugins.AddFromObject(new FaqPlugin(faqRepository));
/// </code>
/// </example>
/// <param name="faqRepository">FAQ 検索用のリポジトリ。</param>
public class FaqPlugin(IFaqRepository faqRepository)
{
    /// <summary>
    /// キーワードで FAQ を検索し、該当する質問と回答を返す。
    /// </summary>
    /// <param name="query">検索キーワード。自然言語での質問も可能。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>
    /// 該当する FAQ がある場合は JSON 形式の質問・回答リスト。
    /// 該当なしの場合はお問い合わせ案内メッセージを返す。
    /// </returns>
    /// <remarks>
    /// <para>
    /// AI モデルがユーザーの質問を分析し、適切なキーワードを抽出してこの関数を呼び出す。
    /// 複数の FAQ が該当する場合は、全てを JSON 配列として返す。
    /// </para>
    /// <para>
    /// <b>レスポンス形式:</b>
    /// <code>
    /// [
    ///   { "Question": "返品はできますか？", "Answer": "購入後30日以内であれば..." },
    ///   { "Question": "返品時の送料は？", "Answer": "お客様負担となります..." }
    /// ]
    /// </code>
    /// </para>
    /// </remarks>
    [KernelFunction("search_faq")]
    [Description("よくある質問（FAQ）を検索します。返品、配送、支払い、サイズ交換などの質問に回答します。")]
    public async Task<string> SearchFaqAsync(
        [Description("検索キーワード")] string query,
        CancellationToken ct = default)
    {
        var results = await faqRepository.SearchAsync(query, ct);

        if (results.Count > 0)
        {
            var formatted = results.Select(r => new { r.Question, r.Answer }).ToList();
            return System.Text.Json.JsonSerializer.Serialize(formatted);
        }

        return "該当するFAQが見つかりませんでした。お問い合わせフォームからご連絡ください。";
    }
}
