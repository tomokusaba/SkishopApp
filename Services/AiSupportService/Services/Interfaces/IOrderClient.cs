namespace AiSupportService.Services.Interfaces;

/// <summary>
/// SalesManagementService の注文 API と通信するクライアントインターフェース。
/// </summary>
/// <remarks>
/// <para>
/// このクライアントは、AI チャットボットがユーザーの注文状況を確認したり、
/// パーソナライズされたレコメンデーションを生成するために使用します。
/// </para>
/// <para>
/// 通信は <see cref="System.Net.Http.IHttpClientFactory"/> を介して行われ、
/// リトライやサーキットブレーカーなどの耐障害性パターンが適用されています。
/// </para>
/// <para>
/// API エラー発生時は、例外をスローせずに <c>null</c> または空のリストを返します。
/// これにより、外部サービスの障害が AI 機能全体の停止につながることを防ぎます。
/// </para>
/// </remarks>
public interface IOrderClient
{
    /// <summary>
    /// 注文 ID とユーザー ID に基づいて注文の詳細を取得する。
    /// </summary>
    /// <param name="orderId">
    /// 取得する注文の ID。
    /// </param>
    /// <param name="userId">
    /// 注文を所有するユーザーの ID。認可チェックに使用されます。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 注文情報。注文が見つからない場合、または API エラーが発生した場合は <c>null</c>。
    /// </returns>
    /// <remarks>
    /// <para>
    /// このメソッドは AI チャットで「注文状況を確認したい」というユーザー要求に
    /// 対応するために使用されます。
    /// </para>
    /// <para>
    /// API 呼び出し: <c>GET /api/v1/orders/{orderId}?userId={userId}</c>
    /// </para>
    /// </remarks>
    Task<OrderInfo?> GetOrderByIdAsync(string orderId, string userId, CancellationToken ct = default);

    /// <summary>
    /// 指定ユーザーの最近の注文一覧を取得する。
    /// </summary>
    /// <param name="userId">
    /// 注文一覧を取得するユーザーの ID。
    /// </param>
    /// <param name="count">
    /// 取得する注文の件数。デフォルトは 5 件。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 注文サマリーのリスト。注文がない場合、または API エラーが発生した場合は空のリスト。
    /// </returns>
    /// <remarks>
    /// <para>
    /// このメソッドはレコメンデーションエンジンでユーザーの購買履歴を参照するため、
    /// または AI チャットで「最近の注文を見せて」という要求に対応するために使用されます。
    /// </para>
    /// <para>
    /// API 呼び出し: <c>GET /api/v1/orders?userId={userId}&amp;count={count}</c>
    /// </para>
    /// </remarks>
    Task<List<OrderSummary>> GetRecentOrdersAsync(string userId, int count = 5, CancellationToken ct = default);
}

/// <summary>
/// 注文の詳細情報を表す DTO。
/// </summary>
/// <param name="Id">注文の一意識別子。</param>
/// <param name="Status">
/// 注文ステータス。PENDING, CONFIRMED, SHIPPED, DELIVERED, CANCELLED などの値を取ります。
/// </param>
/// <param name="TotalAmount">税込み合計金額（日本円）。</param>
/// <param name="CreatedAt">注文が作成された日時（UTC）。</param>
/// <param name="Items">注文に含まれる商品明細のリスト。</param>
/// <remarks>
/// <para>
/// この DTO は SalesManagementService の注文詳細 API のレスポンスに対応します。
/// AI チャットでユーザーに注文内容を説明する際に使用されます。
/// </para>
/// </remarks>
public record OrderInfo(string Id, string Status, decimal TotalAmount, DateTime CreatedAt, List<OrderItemInfo> Items);

/// <summary>
/// 注文明細の情報を表す DTO。
/// </summary>
/// <param name="ProductId">商品の一意識別子。</param>
/// <param name="ProductName">商品の表示名。</param>
/// <param name="Quantity">注文数量。</param>
/// <param name="UnitPrice">商品の単価（税込み、日本円）。</param>
/// <remarks>
/// 合計金額は <c>Quantity * UnitPrice</c> で計算できます。
/// </remarks>
public record OrderItemInfo(string ProductId, string ProductName, int Quantity, decimal UnitPrice);

/// <summary>
/// 注文のサマリー情報を表す DTO。
/// </summary>
/// <param name="Id">注文の一意識別子。</param>
/// <param name="Status">
/// 注文ステータス。PENDING, CONFIRMED, SHIPPED, DELIVERED, CANCELLED などの値を取ります。
/// </param>
/// <param name="TotalAmount">税込み合計金額（日本円）。</param>
/// <param name="CreatedAt">注文が作成された日時（UTC）。</param>
/// <param name="ItemCount">注文に含まれる商品の種類数（明細数）。</param>
/// <remarks>
/// <para>
/// この DTO は注文一覧取得時に使用される軽量なサマリー情報です。
/// 詳細な明細情報が必要な場合は <see cref="IOrderClient.GetOrderByIdAsync"/> を使用してください。
/// </para>
/// </remarks>
public record OrderSummary(string Id, string Status, decimal TotalAmount, DateTime CreatedAt, int ItemCount);
