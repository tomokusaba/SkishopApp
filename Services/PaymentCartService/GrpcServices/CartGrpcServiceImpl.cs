using Grpc.Core;
using PaymentCartService.Exceptions;
using PaymentCartService.Services.Interfaces;
using SkiShop.Contracts.Cart.V1;

namespace PaymentCartService.GrpcServices;

public class CartGrpcServiceImpl(
    ICartService cartService,
    ILogger<CartGrpcServiceImpl> logger) : CartGrpcService.CartGrpcServiceBase
{
    public override async Task<GetCartSnapshotResponse> GetCartSnapshot(
        GetCartSnapshotRequest request,
        ServerCallContext callContext)
    {
        var ct = callContext.CancellationToken;

        logger.LogInformation("gRPC GetCartSnapshot: CartId={CartId}", request.CartId);

        try
        {
            var cart = await cartService.GetCartAsync(request.CartId, ct);

            var response = new GetCartSnapshotResponse
            {
                CartId = cart.Id,
                CustomerId = cart.CustomerId ?? string.Empty,
                TotalAmountMinorUnits = (long)(cart.TotalAmount * 100),
                CurrencyCode = "JPY"
            };

            foreach (var item in cart.Items)
            {
                response.Items.Add(new CartItemSnapshot
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Sku = item.Sku ?? string.Empty,
                    UnitPriceMinorUnits = (long)(item.UnitPrice * 100),
                    Quantity = item.Quantity,
                    SubtotalMinorUnits = (long)(item.Subtotal * 100)
                });
            }

            return response;
        }
        catch (NotFoundException)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "カートが見つかりません"));
        }
        catch (BusinessException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
    }

    public override async Task<ClearCartResponse> ClearCart(
        ClearCartRequest request,
        ServerCallContext callContext)
    {
        var ct = callContext.CancellationToken;

        logger.LogInformation("gRPC ClearCart: CartId={CartId}", request.CartId);

        try
        {
            await cartService.ClearCartAsync(request.CartId, ct);

            logger.LogInformation("カートクリア完了: CartId={CartId}", request.CartId);

            return new ClearCartResponse
            {
                Success = true,
                Message = "カートをクリアしました"
            };
        }
        catch (NotFoundException)
        {
            // べき等: カートが見つからない場合も成功として扱う
            logger.LogInformation("カートが存在しません（べき等応答）: CartId={CartId}", request.CartId);
            return new ClearCartResponse
            {
                Success = true,
                Message = "カートは既にクリア済みです"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "gRPC ClearCart 失敗: CartId={CartId}", request.CartId);
            throw new RpcException(new Status(StatusCode.Internal, "カートクリアに失敗しました"));
        }
    }
}
