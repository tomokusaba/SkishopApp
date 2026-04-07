using Frontend.Store.CartStore;
using Shouldly;
using Xunit;

namespace Frontend.Tests.Store;

[Trait("Category", "Unit")]
public class CartStoreTests
{
    [Fact]
    public void Should_AddItem_When_AddToCartAction()
    {
        // Arrange
        var state = new CartState();
        var action = new AddToCartAction("prod-1", "スキーブーツ", 29800m, 1, null, 10);

        // Act
        var newState = CartReducers.OnAddToCart(state, action);

        // Assert
        newState.Items.Count.ShouldBe(1);
        newState.Items[0].ProductId.ShouldBe("prod-1");
        newState.Items[0].ProductName.ShouldBe("スキーブーツ");
        newState.Items[0].Price.ShouldBe(29800m);
        newState.Items[0].Quantity.ShouldBe(1);
    }

    [Fact]
    public void Should_RemoveItem_When_RemoveFromCartAction()
    {
        // Arrange
        var state = new CartState
        {
            Items = [new CartItem("prod-1", "スキーブーツ", 29800m, 1)]
        };
        var action = new RemoveFromCartAction("prod-1");

        // Act
        var newState = CartReducers.OnRemoveFromCart(state, action);

        // Assert
        newState.Items.Count.ShouldBe(0);
    }

    [Fact]
    public void Should_UpdateQuantity_When_UpdateQuantityAction()
    {
        // Arrange
        var state = new CartState
        {
            Items = [new CartItem("prod-1", "スキーブーツ", 29800m, 1)]
        };
        var action = new UpdateCartItemQuantityAction("prod-1", 3);

        // Act
        var newState = CartReducers.OnUpdateQuantity(state, action);

        // Assert
        newState.Items.Count.ShouldBe(1);
        var item = newState.Items.First(i => i.ProductId == "prod-1");
        item.Quantity.ShouldBe(3);
    }

    [Fact]
    public void Should_ClearCart_When_ClearCartAction()
    {
        // Arrange
        var state = new CartState
        {
            Items =
            [
                new CartItem("prod-1", "スキーブーツ", 29800m, 1),
                new CartItem("prod-2", "ゴーグル", 12000m, 2)
            ]
        };

        // Act
        var newState = CartReducers.OnClearCart(state);

        // Assert
        newState.Items.Count.ShouldBe(0);
    }

    [Fact]
    public void Should_SetLoading_When_FetchCartAction()
    {
        // Arrange
        var state = new CartState();

        // Act
        var newState = CartReducers.OnLoadCart(state);

        // Assert
        newState.IsLoading.ShouldBeTrue();
        newState.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Should_SetItems_When_FetchCartResultAction()
    {
        // Arrange
        var state = new CartState { IsLoading = true };
        var items = new List<CartItem>
        {
            new("prod-1", "スキーブーツ", 29800m, 1),
            new("prod-2", "ゴーグル", 12000m, 2)
        };
        var action = new LoadCartSuccessAction(items);

        // Act
        var newState = CartReducers.OnLoadCartSuccess(state, action);

        // Assert
        newState.IsLoading.ShouldBeFalse();
        newState.Items.Count.ShouldBe(2);
        newState.Items[0].ProductId.ShouldBe("prod-1");
        newState.Items[1].ProductId.ShouldBe("prod-2");
    }
}
