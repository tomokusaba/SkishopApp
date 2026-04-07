namespace AiSupportService.Models;

/// <summary>
/// レコメンデーション種別を定義する列挙型。
/// </summary>
/// <remarks>
/// <para>
/// この列挙型は <see cref="Recommendation"/> エンティティの種別を表現します。
/// データベースには大文字の文字列形式（例: <c>"PERSONALIZED"</c>）で格納されます。
/// </para>
/// <para>
/// 各種別は異なるアルゴリズムと有効期限を持ちます。
/// 適切なファクトリメソッド（<see cref="Recommendation.CreatePersonalized"/> 等）を使用して
/// レコメンデーションを作成してください。
/// </para>
/// </remarks>
/// <seealso cref="Recommendation"/>
/// <seealso cref="Recommendation.TypeEnum"/>
public enum RecommendationType
{
    /// <summary>
    /// パーソナライズされたおすすめ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ユーザーの閲覧履歴、購買履歴、好みに基づいて生成される個人向けレコメンデーションです。
    /// 協調フィルタリングとコンテンツベースフィルタリングのハイブリッドアルゴリズムを使用します。
    /// </para>
    /// <para>
    /// 有効期限: 1 日（ユーザー行動の変化に追従するため短めに設定）
    /// </para>
    /// </remarks>
    Personalized,

    /// <summary>
    /// トレンド商品。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 現在人気のある商品のレコメンデーションです。
    /// 直近の閲覧数、販売数、カート追加数などの指標に基づいて生成されます。
    /// </para>
    /// <para>
    /// 有効期限: 6 時間（トレンドの変化に迅速に対応するため）
    /// </para>
    /// </remarks>
    Trending,

    /// <summary>
    /// 類似商品。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ユーザーが閲覧した商品と類似した商品のレコメンデーションです。
    /// 商品の埋め込みベクトル（<see cref="ProductDocument.Embedding"/>）の
    /// コサイン類似度に基づいて生成されます。
    /// </para>
    /// <para>
    /// 有効期限: 7 日（商品の類似性は比較的安定しているため）
    /// </para>
    /// </remarks>
    Similar,

    /// <summary>
    /// 季節のおすすめ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 現在の季節やイベントに適した商品のレコメンデーションです。
    /// スキーシーズン、セール時期などに合わせて生成されます。
    /// </para>
    /// <para>
    /// 有効期限: 30 日（季節の変わり目まで有効）
    /// </para>
    /// </remarks>
    Seasonal,

    /// <summary>
    /// よく一緒に購入される商品。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ユーザーがカートに入れた商品や購入した商品と一緒に購入されることが多い商品のレコメンデーションです。
    /// 購買パターン分析（マーケットバスケット分析）に基づいて生成されます。
    /// </para>
    /// <para>
    /// 例: スキー板を見ているユーザーにビンディングやストックを推薦
    /// </para>
    /// <para>
    /// 有効期限: 14 日
    /// </para>
    /// </remarks>
    FrequentlyBoughtTogether
}
