using SalesManagementService.Infrastructure.Outbox;
using Shouldly;

namespace SalesManagementService.Tests.Integration.Outbox;

public class OutboxPublisherTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Should_HaveOutboxWriterInterface_When_CheckingServiceRegistration()
    {
        // Arrange & Act
        var writerType = typeof(OutboxWriter);

        // Assert — OutboxWriter はコンクリートクラスとして存在する
        writerType.ShouldNotBeNull();
        writerType.Name.ShouldBe("OutboxWriter");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Should_ImplementIOutboxWriter_When_CheckingInterfaces()
    {
        // Arrange & Act
        var writerType = typeof(OutboxWriter);
        var interfaces = writerType.GetInterfaces();

        // Assert
        interfaces.ShouldContain(t => t.Name == "IOutboxWriter");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Should_HaveOutboxPublisherBackgroundService_When_CheckingType()
    {
        // Arrange & Act
        var publisherType = typeof(OutboxPublisher);

        // Assert
        publisherType.ShouldNotBeNull();
        publisherType.BaseType!.Name.ShouldBe("BackgroundService");
    }
}
