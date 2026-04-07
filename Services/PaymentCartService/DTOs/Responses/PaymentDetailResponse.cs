namespace PaymentCartService.DTOs.Responses;

/// <summary>
/// 決済の詳細情報レスポンス
/// </summary>
/// <param name="Id">決済 ID</param>
/// <param name="OrderId">注文 ID</param>
/// <param name="CustomerId">顧客 ID</param>
/// <param name="Amount">決済金額</param>
/// <param name="CurrencyCode">通貨コード（ISO 4217）</param>
/// <param name="Status">決済ステータス</param>
/// <param name="PaymentMethod">決済方法</param>
/// <param name="StripeCheckoutSessionId">Stripe チェックアウトセッション ID</param>
/// <param name="StripePaymentIntentId">Stripe 決済インテント ID</param>
/// <param name="PaidAt">支払完了日時</param>
/// <param name="CreatedAt">作成日時</param>
/// <param name="UpdatedAt">更新日時</param>
public record PaymentDetailResponse(
    string Id,
    string OrderId,
    string CustomerId,
    decimal Amount,
    string CurrencyCode,
    string Status,
    string PaymentMethod,
    string? StripeCheckoutSessionId,
    string? StripePaymentIntentId,
    DateTime? PaidAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
