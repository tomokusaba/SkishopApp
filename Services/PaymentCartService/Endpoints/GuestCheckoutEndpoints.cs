using FluentValidation;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Endpoints;

public static class GuestCheckoutEndpoints
{
    private const string CartIdCookieName = "CartId";

    public static void MapGuestCheckoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/checkout")
            .WithTags("Checkout");

        group.MapPost("/guest", GuestCheckout)
            .AllowAnonymous()
            .RequireRateLimiting("checkout")
            .WithName("GuestCheckout");
    }

    private static async Task<IResult> GuestCheckout(
        HttpContext httpContext,
        GuestCheckoutRequest request,
        IValidator<GuestCheckoutRequest> validator,
        IPaymentService paymentService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var cookieCartId = httpContext.Request.Cookies[CartIdCookieName];
        var requestCartId = request.CartId;

        // Cookie を優先（セキュリティ上、Cookie は HttpOnly で保護されているため）
        var cartId = cookieCartId ?? requestCartId;

        if (string.IsNullOrWhiteSpace(cartId))
            return Results.BadRequest("カート ID が特定できません");

        // Cookie とリクエストでカート ID が異なる場合は警告ログ
        if (cookieCartId is not null && requestCartId is not null && cookieCartId != requestCartId)
        {
            logger.LogWarning(
                "カート ID 不一致: Cookie={CookieCartId}, Request={RequestCartId}. Cookie を優先します",
                cookieCartId, requestCartId);
        }

        var result = await paymentService.GuestCheckoutAsync(cartId, request, ct);
        return Results.Ok(result);
    }
}
