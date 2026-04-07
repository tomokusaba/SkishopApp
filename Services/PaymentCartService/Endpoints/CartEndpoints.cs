using System.Security.Claims;
using System.Threading.RateLimiting;
using FluentValidation;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.Exceptions;
using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Endpoints;

public static class CartEndpoints
{
    private const string CartIdCookieName = "CartId";
    private const string SessionTokenCookieName = "SessionToken";

    public static void MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/cart")
            .WithTags("Cart")
            .RequireRateLimiting("cart");

        group.MapGet("/", GetCart)
            .AllowAnonymous()
            .WithName("GetCart");

        group.MapPost("/items", AddItem)
            .AllowAnonymous()
            .WithName("AddCartItem");

        group.MapPut("/items/{itemId}", UpdateItemQuantity)
            .AllowAnonymous()
            .WithName("UpdateCartItem");

        group.MapDelete("/items/{itemId}", RemoveItem)
            .AllowAnonymous()
            .WithName("RemoveCartItem");

        group.MapDelete("/", ClearCart)
            .AllowAnonymous()
            .WithName("ClearCart");

        group.MapPost("/merge", MergeCart)
            .RequireAuthorization()
            .WithName("MergeCart");
    }

    private static async Task<IResult> GetCart(
        HttpContext httpContext,
        ICartService cartService,
        CancellationToken ct)
    {
        var cartId = httpContext.Request.Cookies[CartIdCookieName];
        var sessionId = GetOrCreateSessionToken(httpContext);

        var cart = await cartService.GetOrCreateCartAsync(cartId, sessionId, ct);

        SetCartCookie(httpContext, cart.Id);
        return Results.Ok(cart);
    }

    private static async Task<IResult> AddItem(
        HttpContext httpContext,
        AddCartItemRequest request,
        IValidator<AddCartItemRequest> validator,
        ICartService cartService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var cartId = httpContext.Request.Cookies[CartIdCookieName];
        var sessionId = GetOrCreateSessionToken(httpContext);

        var existingCart = await cartService.GetOrCreateCartAsync(cartId, sessionId, ct);
        var cart = await cartService.AddItemAsync(existingCart.Id, request, ct);

        SetCartCookie(httpContext, cart.Id);
        return Results.Ok(cart);
    }

    private static async Task<IResult> UpdateItemQuantity(
        string itemId,
        HttpContext httpContext,
        UpdateCartItemRequest request,
        IValidator<UpdateCartItemRequest> validator,
        ICartService cartService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var cartId = httpContext.Request.Cookies[CartIdCookieName]
            ?? throw new NotFoundException("カートが見つかりません");

        var cart = await cartService.UpdateItemQuantityAsync(cartId, itemId, request, ct);
        return Results.Ok(cart);
    }

    private static async Task<IResult> RemoveItem(
        string itemId,
        HttpContext httpContext,
        ICartService cartService,
        CancellationToken ct)
    {
        var cartId = httpContext.Request.Cookies[CartIdCookieName]
            ?? throw new NotFoundException("カートが見つかりません");

        var cart = await cartService.RemoveItemAsync(cartId, itemId, ct);
        return Results.Ok(cart);
    }

    private static async Task<IResult> ClearCart(
        HttpContext httpContext,
        ICartService cartService,
        CancellationToken ct)
    {
        var cartId = httpContext.Request.Cookies[CartIdCookieName];
        if (cartId is null)
            return Results.NoContent();

        await cartService.ClearCartAsync(cartId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> MergeCart(
        MergeCartRequest request,
        IValidator<MergeCartRequest> validator,
        ClaimsPrincipal user,
        ICartService cartService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new BusinessException("認証情報が無効です");

        var cart = await cartService.MergeCartAsync(request.GuestCartId, userId, ct);
        return Results.Ok(cart);
    }

    /// <summary>
    /// Cookie ベースのセッショントークンを取得または生成
    /// httpContext.Connection.Id は接続ごとに変化するため使用不可
    /// </summary>
    private static string GetOrCreateSessionToken(HttpContext httpContext)
    {
        var sessionToken = httpContext.Request.Cookies[SessionTokenCookieName];

        if (string.IsNullOrEmpty(sessionToken))
        {
            sessionToken = Guid.NewGuid().ToString();
            httpContext.Response.Cookies.Append(SessionTokenCookieName, sessionToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromDays(30) // セッショントークンは長めに保持
            });
        }

        return sessionToken;
    }

    private static void SetCartCookie(HttpContext httpContext, string cartId)
    {
        httpContext.Response.Cookies.Append(CartIdCookieName, cartId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(7)
        });
    }
}
