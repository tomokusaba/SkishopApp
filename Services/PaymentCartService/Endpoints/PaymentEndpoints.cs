using System.Security.Claims;
using System.Threading.RateLimiting;
using FluentValidation;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.Exceptions;
using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/payments")
            .WithTags("Payments");

        group.MapPost("/checkout", Checkout)
            .RequireAuthorization()
            .RequireRateLimiting("checkout")
            .WithName("Checkout");

        group.MapGet("/{paymentId}", GetPaymentById)
            .RequireAuthorization()
            .WithName("GetPaymentById");

        group.MapGet("/order/{orderId}", GetPaymentByOrderId)
            .RequireAuthorization()
            .WithName("GetPaymentByOrderId");

        group.MapGet("/customer/{customerId}", GetCustomerPayments)
            .RequireAuthorization()
            .WithName("GetCustomerPayments");

        group.MapPost("/{paymentId}/refund", Refund)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("refund")
            .WithName("RefundPayment");

        group.MapPost("/webhook", HandleWebhook)
            .AllowAnonymous()
            .RequireRateLimiting("webhook")
            .WithName("StripeWebhook");
    }

    private static async Task<IResult> Checkout(
        CheckoutRequest request,
        IValidator<CheckoutRequest> validator,
        ClaimsPrincipal user,
        IPaymentService paymentService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new BusinessException("認証情報が無効です");

        var payment = await paymentService.CheckoutAsync(request, userId, ct);
        return Results.Created($"/api/v1/payments/{payment.Id}", payment);
    }

    private static async Task<IResult> GetPaymentById(
        string paymentId,
        ClaimsPrincipal user,
        IPaymentService paymentService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new BusinessException("認証情報が無効です");

        return await paymentService.GetByIdAsync(paymentId, userId, ct) is { } payment
            ? Results.Ok(payment)
            : Results.NotFound();
    }

    private static async Task<IResult> GetPaymentByOrderId(
        string orderId,
        ClaimsPrincipal user,
        IPaymentService paymentService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new BusinessException("認証情報が無効です");

        return await paymentService.GetByOrderIdAsync(orderId, userId, ct) is { } payment
            ? Results.Ok(payment)
            : Results.NotFound();
    }

    private static async Task<IResult> GetCustomerPayments(
        string customerId,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        IPaymentService paymentService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new BusinessException("認証情報が無効です");

        var result = await paymentService.GetByCustomerIdAsync(
            customerId, userId, page > 0 ? page : 1, pageSize > 0 ? Math.Min(pageSize, 100) : 20, ct);

        return Results.Ok(result);
    }

    private static async Task<IResult> Refund(
        string paymentId,
        RefundRequest request,
        IValidator<RefundRequest> validator,
        ClaimsPrincipal user,
        IRefundService refundService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var adminUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new BusinessException("認証情報が無効です");

        var result = await refundService.RefundAsync(paymentId, request, adminUserId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleWebhook(
        HttpContext httpContext,
        IPaymentService paymentService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var json = await new StreamReader(httpContext.Request.Body).ReadToEndAsync(ct);
        var signature = httpContext.Request.Headers["Stripe-Signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature))
        {
            logger.LogWarning("Stripe Webhook: Stripe-Signature ヘッダーがありません");
            return Results.BadRequest();
        }

        try
        {
            await paymentService.HandleWebhookAsync(json, signature, ct);
            return Results.Ok();
        }
        catch (Stripe.StripeException ex)
        {
            logger.LogWarning(ex, "Stripe Webhook 署名検証失敗");
            return Results.BadRequest();
        }
    }
}
