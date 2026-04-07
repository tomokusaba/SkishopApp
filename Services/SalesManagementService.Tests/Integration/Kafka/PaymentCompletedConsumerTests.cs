using SalesManagementService.Infrastructure.Kafka;
using Shouldly;

namespace SalesManagementService.Tests.Integration.Kafka;

public class PaymentCompletedConsumerTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Should_BeBackgroundService_When_CheckingType()
    {
        // Arrange & Act
        var consumerType = typeof(PaymentCompletedConsumer);

        // Assert
        consumerType.ShouldNotBeNull();
        consumerType.BaseType!.Name.ShouldBe("BackgroundService");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Should_HaveExecuteAsyncMethod_When_CheckingMembers()
    {
        // Arrange & Act
        var method = typeof(PaymentCompletedConsumer).GetMethod(
            "ExecuteAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Assert
        method.ShouldNotBeNull();
        method.ReturnType.ShouldBe(typeof(Task));
    }
}
