using AiSupportService.Repositories.Interfaces;

namespace AiSupportService.Repositories;

/// <summary>
/// <see cref="IFaqRepository"/> のインメモリ実装。静的な FAQ データを保持する。
/// </summary>
/// <remarks>
/// <para>
/// <strong>データストレージ:</strong>
/// 現在の実装ではインメモリの静的 Dictionary を使用しています。
/// FAQ データはアプリケーション起動時に初期化され、実行中は変更されません。
/// </para>
/// <para>
/// <strong>パフォーマンス考慮:</strong>
/// <list type="bullet">
///   <item><description>インメモリ検索のため、データベースアクセスのオーバーヘッドなし</description></item>
///   <item><description>LINQ の Where フィルタリングで O(n) の時間計算量</description></item>
///   <item><description>データ量が少ない（12 件程度）ため、線形検索で十分高速</description></item>
///   <item><description>Task.FromResult で同期的に結果を返すため、非同期オーバーヘッドなし</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>将来の拡張:</strong>
/// データベースや外部 CMS との連携が必要な場合、このクラスを継承または
/// 新しい実装クラスに置き換えてください。インターフェースを通じて
/// DI コンテナで切り替え可能です。
/// </para>
/// <para>
/// <strong>エラーハンドリング:</strong>
/// インメモリ操作のため、例外が発生する可能性は低いです。
/// null や空文字列の検索クエリに対しても安全に動作します。
/// </para>
/// </remarks>
public class FaqRepository : IFaqRepository
{
    /// <summary>
    /// FAQ データを格納する静的辞書。
    /// キーは質問のキーワード、値は回答文です。
    /// </summary>
    private static readonly Dictionary<string, string> FaqData = new()
    {
        ["返品ポリシー"] = "商品到着後14日以内であれば、未使用・未開封の商品に限り返品を承ります。返品送料はお客様負担となります。",
        ["送料"] = "ご注文金額が10,000円以上の場合、送料無料です。10,000円未満の場合は全国一律880円の送料がかかります。",
        ["配送日数"] = "通常、ご注文後2〜5営業日でお届けいたします。北海道・沖縄・離島は追加で2〜3日かかる場合があります。",
        ["支払い方法"] = "クレジットカード（VISA、MasterCard、JCB、AMEX）、銀行振込、代金引換をご利用いただけます。",
        ["サイズ交換"] = "サイズ交換は商品到着後14日以内にお申し付けください。在庫がある場合は無料で交換いたします。",
        ["会員登録"] = "会員登録は無料です。登録すると購入履歴の管理、お気に入り商品の保存、ポイントプログラムの利用が可能になります。",
        ["ポイント制度"] = "お買い物金額の1%がポイントとして付与されます。1ポイント=1円としてご利用いただけます。",
        ["クーポン"] = "メールマガジン登録で初回10%オフクーポンをプレゼント。季節ごとのセールでもクーポンを配布しています。",
        ["商品保証"] = "スキー板・ブーツは購入日から2年間のメーカー保証付きです。製造上の欠陥が認められた場合、無償で修理・交換いたします。",
        ["メンテナンス"] = "スキー板のワックスがけ、エッジ研磨などのメンテナンスサービスは店舗にて承っております。",
        ["ギフトラッピング"] = "ギフトラッピングは1点あたり330円で承ります。ご注文時にオプションからお選びください。",
        ["在庫確認"] = "商品ページに在庫状況が表示されています。「在庫あり」の商品は通常即日発送可能です。「お取り寄せ」の場合は5〜10営業日かかります。"
    };

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>検索ロジック:</strong>
    /// 以下の条件のいずれかを満たす FAQ を返します：
    /// <list type="bullet">
    ///   <item><description>キーワード（Question）にクエリ文字列が含まれる（大文字小文字を区別しない）</description></item>
    ///   <item><description>回答（Answer）にクエリ文字列が含まれる（大文字小文字を区別しない）</description></item>
    ///   <item><description>クエリ文字列にキーワードが含まれる（逆方向マッチ）</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// インメモリの LINQ フィルタリングで処理されます。
    /// データ量が少ないため、線形検索で十分高速です。
    /// </para>
    /// </remarks>
    public Task<List<(string Question, string Answer)>> SearchAsync(string query, CancellationToken ct = default)
    {
        var normalizedQuery = query.ToLowerInvariant();
        var results = FaqData
            .Where(kvp => kvp.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                       || kvp.Value.Contains(query, StringComparison.OrdinalIgnoreCase)
                       || normalizedQuery.Contains(kvp.Key.ToLowerInvariant()))
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();

        return Task.FromResult(results);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// インメモリの静的辞書から全データを取得します。
    /// データベースアクセスは発生しません。
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// 全データをメモリから読み取るため、非常に高速です。
    /// Task.FromResult で同期的に結果を返します。
    /// </para>
    /// </remarks>
    public Task<List<(string Question, string Answer)>> GetAllAsync(CancellationToken ct = default)
    {
        var results = FaqData.Select(kvp => (kvp.Key, kvp.Value)).ToList();
        return Task.FromResult(results);
    }
}
