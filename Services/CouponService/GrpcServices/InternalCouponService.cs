using CouponService.DTOs.Requests;
using CouponService.Services.Interfaces;

namespace CouponService.GrpcServices;

/// <summary>
/// 内部サービス間通信用のクーポン操作クラス (設計書 §10 — Saga ステップ 3)
/// TODO: proto ファイルからの自動生成ベースクラスに移行予定
/// TODO: proto/gRPC インフラ整備後に本クラスを gRPC サービスとして有効化する
/// </summary>
public class InternalCouponService(
    ICouponService couponService,
    ILogger<InternalCouponService> logger)
{
    /// <summary>
    /// ApplyCoupon RPC: クーポン検証・適用 (Deadline: 300ms)
    /// </summary>
    public async Task<ApplyCouponResult> ApplyCouponAsync(
        string couponCode, string userId, string orderId,
        decimal orderAmount, CancellationToken ct = default)
    {
        logger.LogInformation(
            "gRPC ApplyCoupon: CouponCode={CouponCode}, UserId={UserId}, OrderId={OrderId}",
            couponCode, userId, orderId);

        var result = await couponService.ValidateAndApplyAsync(
            couponCode, userId, orderAmount, null, ct);

        return new ApplyCouponResult(
            result.IsValid,
            result.ErrorMessage ?? string.Empty,
            result.DiscountAmount);
    }

    /// <summary>
    /// ReleaseCoupon RPC: クーポン利用取消 — 補償トランザクション (Deadline: 500ms)
    /// </summary>
    public async Task<bool> ReleaseCouponAsync(
        string couponId, string orderId, CancellationToken ct = default)
    {
        logger.LogInformation(
            "gRPC ReleaseCoupon: CouponId={CouponId}, OrderId={OrderId}",
            couponId, orderId);

        await couponService.ReleaseCouponAsync(
            new ReleaseCouponRequest(couponId, orderId), ct);

        return true;
    }
}

public record ApplyCouponResult(bool Success, string Message, decimal DiscountAmount);
