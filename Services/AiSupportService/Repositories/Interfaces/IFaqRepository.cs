namespace AiSupportService.Repositories.Interfaces;

/// <summary>
/// Repository for FAQ (Frequently Asked Questions) data access.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Aggregate Root:</strong> FAQ（読み取り専用データ）
/// </para>
/// <para>
/// このリポジトリはよくある質問（FAQ）データへのアクセスを提供します。
/// AI チャットボットが顧客からの問い合わせに対して、
/// 関連する FAQ を検索・提示するために使用されます。
/// </para>
/// <para>
/// <strong>主な責務:</strong>
/// <list type="bullet">
///   <item><description>キーワードベースの FAQ 検索</description></item>
///   <item><description>全 FAQ データの取得</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>実装の特性:</strong>
/// 現在の実装ではインメモリの静的データを使用しています。
/// 将来的にはデータベースや外部 CMS との連携が想定されます。
/// </para>
/// </remarks>
public interface IFaqRepository
{
    /// <summary>
    /// 指定されたクエリに一致する FAQ を検索する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// AI チャットボットが顧客の質問に関連する FAQ を検索するために使用されます。
    /// 検索は質問文と回答文の両方に対して実行されます。
    /// </para>
    /// <para>
    /// <strong>検索ロジック:</strong>
    /// <list type="bullet">
    ///   <item><description>大文字小文字を区別しない部分一致検索</description></item>
    ///   <item><description>質問文（Question）と回答文（Answer）の両方を検索対象とする</description></item>
    ///   <item><description>クエリが FAQ のキーワードに含まれる場合もマッチする</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>マッチする FAQ がない場合は空リストを返す</description></item>
    ///   <item><description>空文字列や null を指定した場合の動作は実装依存</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="query">検索クエリ文字列。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>質問と回答のタプルリスト。</returns>
    Task<List<(string Question, string Answer)>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// 全ての FAQ を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// FAQ 一覧ページの表示や、AI モデルへのコンテキスト提供に使用されます。
    /// 全データをメモリに読み込むため、大量の FAQ がある場合は注意が必要です。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>登録されている全ての FAQ を返す</description></item>
    ///   <item><description>FAQ が存在しない場合は空リストを返す</description></item>
    ///   <item><description>順序は実装依存（特定の順序保証なし）</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>質問と回答のタプルリスト。</returns>
    Task<List<(string Question, string Answer)>> GetAllAsync(CancellationToken ct = default);
}
