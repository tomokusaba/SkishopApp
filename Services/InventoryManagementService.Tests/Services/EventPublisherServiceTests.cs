using Xunit;
using InventoryManagementService.Infrastructure.Persistence;
using InventoryManagementService.Models;
using InventoryManagementService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace InventoryManagementService.Tests.Services;

/// <summary>
/// <see cref="EventPublisherService"/> の単体テスト。
/// InMemory DbContext を使用し、Outbox パターンによるイベントルーティング・永続化ロジックを検証する。
/// </summary>
[Trait("Category", "Unit")]
public class EventPublisherServiceTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options, TimeProvider.System);
    }

    [Fact]
    public async Task Should_AddOutboxEvent_When_PublishAsyncCalled()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var logger = Substitute.For<ILogger<EventPublisherService>>();
        var sut = new EventPublisherService(context, logger);
        var payload = new { OrderId = "order-1", Amount = 100m };

        // Act
        await sut.PublishAsync("TestEvent", "test.topic", "agg-1", "TestAggregate", payload);

        // Assert — verify entity was added to change tracker (without calling SaveChangesAsync
        // which has DateTime/DateTimeOffset incompatibility with InMemory provider)
        var addedEntries = context.ChangeTracker.Entries<OutboxEvent>().ToList();
        addedEntries.Count.ShouldBe(1);
        addedEntries[0].Entity.EventType.ShouldBe("TestEvent");
        addedEntries[0].Entity.Topic.ShouldBe("test.topic");
        addedEntries[0].Entity.AggregateId.ShouldBe("agg-1");
        addedEntries[0].Entity.AggregateType.ShouldBe("TestAggregate");
        addedEntries[0].Entity.Status.ShouldBe("PENDING");
    }

    [Fact]
    public async Task Should_PublishToProductTopic_When_PublishProductEventCalled()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var logger = Substitute.For<ILogger<EventPublisherService>>();
        var sut = new EventPublisherService(context, logger);
        var payload = new { ProductId = "prod-1" };

        // Act
        await sut.PublishProductEventAsync("ProductCreated", "prod-1", payload);

        // Assert
        var addedEntries = context.ChangeTracker.Entries<OutboxEvent>().ToList();
        addedEntries.Count.ShouldBe(1);
        addedEntries[0].Entity.Topic.ShouldBe("inventory.products");
        addedEntries[0].Entity.AggregateType.ShouldBe("Product");
    }

    [Fact]
    public async Task Should_PublishToInventoryTopic_When_PublishInventoryEventCalled()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var logger = Substitute.For<ILogger<EventPublisherService>>();
        var sut = new EventPublisherService(context, logger);
        var payload = new { ProductId = "prod-1", Quantity = 50 };

        // Act
        await sut.PublishInventoryEventAsync("InventoryUpdated", "prod-1", payload);

        // Assert
        var addedEntries = context.ChangeTracker.Entries<OutboxEvent>().ToList();
        addedEntries.Count.ShouldBe(1);
        addedEntries[0].Entity.Topic.ShouldBe("inventory.levels");
        addedEntries[0].Entity.AggregateType.ShouldBe("Inventory");
    }

    [Fact]
    public async Task Should_PublishToPriceTopic_When_PublishPriceEventCalled()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var logger = Substitute.For<ILogger<EventPublisherService>>();
        var sut = new EventPublisherService(context, logger);
        var payload = new { ProductId = "prod-1", NewPrice = 29800m };

        // Act
        await sut.PublishPriceEventAsync("PriceUpdated", "prod-1", payload);

        // Assert
        var addedEntries = context.ChangeTracker.Entries<OutboxEvent>().ToList();
        addedEntries.Count.ShouldBe(1);
        addedEntries[0].Entity.Topic.ShouldBe("inventory.pricing");
        addedEntries[0].Entity.AggregateType.ShouldBe("Price");
    }
}
