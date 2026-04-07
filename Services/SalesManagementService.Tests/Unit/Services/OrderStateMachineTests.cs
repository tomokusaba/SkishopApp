using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Models;
using SalesManagementService.Models.Enums;
using SalesManagementService.Services;
using Shouldly;

namespace SalesManagementService.Tests.Unit.Services;

public class OrderStateMachineTests
{
    // ── 有効な遷移テスト ──

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("PENDING", "CONFIRMED")]
    [InlineData("PENDING", "CANCELLED")]
    [InlineData("PENDING", "PENDING_PAYMENT")]
    [InlineData("PENDING", "PAYMENT_FAILED")]
    [InlineData("PENDING_PAYMENT", "CONFIRMED")]
    [InlineData("PENDING_PAYMENT", "CANCELLED")]
    [InlineData("PENDING_PAYMENT", "PAYMENT_FAILED")]
    [InlineData("CONFIRMED", "PROCESSING")]
    [InlineData("CONFIRMED", "CANCELLED")]
    [InlineData("PROCESSING", "SHIPPED")]
    [InlineData("PROCESSING", "CANCELLED")]
    [InlineData("PROCESSING", "INVENTORY_SHORTAGE")]
    [InlineData("SHIPPED", "DELIVERED")]
    [InlineData("DELIVERED", "RETURNED")]
    [InlineData("RETURNED", "REFUNDED")]
    [InlineData("INVENTORY_SHORTAGE", "PROCESSING")]
    [InlineData("INVENTORY_SHORTAGE", "CANCELLED")]
    [InlineData("PAYMENT_FAILED", "PENDING_PAYMENT")]
    [InlineData("PAYMENT_FAILED", "CANCELLED")]
    public void Should_ReturnTrue_When_ValidTransition(string from, string to)
    {
        // Arrange & Act
        var result = OrderStateMachine.CanTransition(from, to);

        // Assert
        result.ShouldBeTrue($"Transition from '{from}' to '{to}' should be valid");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("PENDING", "CONFIRMED")]
    [InlineData("CONFIRMED", "PROCESSING")]
    [InlineData("PROCESSING", "SHIPPED")]
    [InlineData("SHIPPED", "DELIVERED")]
    public void Should_NotThrow_When_ValidTransitionValidated(string from, string to)
    {
        // Arrange & Act & Assert
        Should.NotThrow(() => OrderStateMachine.ValidateTransition(from, to));
    }

    // ── 無効な遷移テスト ──

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("DELIVERED", "PENDING")]
    [InlineData("CANCELLED", "CONFIRMED")]
    [InlineData("REFUNDED", "PENDING")]
    [InlineData("REFUNDED", "CANCELLED")]
    [InlineData("CANCELLED", "PROCESSING")]
    [InlineData("SHIPPED", "PENDING")]
    [InlineData("SHIPPED", "CANCELLED")]
    [InlineData("DELIVERED", "CANCELLED")]
    [InlineData("PENDING", "SHIPPED")]
    [InlineData("PENDING", "DELIVERED")]
    [InlineData("CONFIRMED", "DELIVERED")]
    [InlineData("PROCESSING", "CONFIRMED")]
    public void Should_ReturnFalse_When_InvalidTransition(string from, string to)
    {
        // Arrange & Act
        var result = OrderStateMachine.CanTransition(from, to);

        // Assert
        result.ShouldBeFalse($"Transition from '{from}' to '{to}' should be invalid");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("DELIVERED", "PENDING")]
    [InlineData("CANCELLED", "CONFIRMED")]
    [InlineData("REFUNDED", "PENDING")]
    public void Should_ThrowInvalidOrderStateException_When_InvalidTransitionValidated(string from, string to)
    {
        // Arrange & Act & Assert
        var ex = Should.Throw<InvalidOrderStateException>(
            () => OrderStateMachine.ValidateTransition(from, to));
        ex.Message.ShouldContain(from);
        ex.Message.ShouldContain(to);
    }

    // ── 終端ステータス ──

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("REFUNDED")]
    [InlineData("CANCELLED")]
    public void Should_ReturnFalse_When_TransitionFromTerminalStatus(string terminalStatus)
    {
        // Arrange
        var possibleTargets = new[]
        {
            "PENDING", "CONFIRMED", "PROCESSING", "SHIPPED",
            "DELIVERED", "RETURNED", "REFUNDED", "CANCELLED"
        };

        // Act & Assert
        foreach (var target in possibleTargets)
        {
            OrderStateMachine.CanTransition(terminalStatus, target)
                .ShouldBeFalse($"Terminal status '{terminalStatus}' should not transition to '{target}'");
        }
    }

    // ── 不明ステータス ──

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ReturnFalse_When_UnknownStatus()
    {
        // Arrange & Act
        var result = OrderStateMachine.CanTransition("UNKNOWN_STATUS", "CONFIRMED");

        // Assert
        result.ShouldBeFalse();
    }

    // ── TransitionTo メソッド ──

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_UpdateOrderStatus_When_TransitionToValidStatus()
    {
        // Arrange
        var order = new Order { Status = "PENDING" };

        // Act
        OrderStateMachine.TransitionTo(order, OrderStatus.Confirmed);

        // Assert
        order.Status.ShouldBe("CONFIRMED");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ThrowInvalidOrderStateException_When_TransitionToInvalidStatus()
    {
        // Arrange
        var order = new Order { Status = "CANCELLED" };

        // Act & Assert
        Should.Throw<InvalidOrderStateException>(
            () => OrderStateMachine.TransitionTo(order, OrderStatus.Confirmed));
    }
}
