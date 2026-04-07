namespace PaymentCartService.DTOs.Responses;

/// <summary>
/// 返金処理のレスポンス
/// </summary>
/// <param name="Id">返金 ID</param>
/// <param name="PaymentId">元の決済 ID</param>
/// <param name="RefundAmount">返金金額</param>
/// <param name="Status">返金ステータス</param>
/// <param name="CreatedAt">作成日時</param>
public record RefundResponse(
    string Id,
    string PaymentId,
    decimal RefundAmount,
    string Status,
    DateTime CreatedAt);
