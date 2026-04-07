using Confluent.Kafka;
using InventoryManagementService.BackgroundServices;
using InventoryManagementService.Infrastructure.Persistence;
using InventoryManagementService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace InventoryManagementService.Tests.Services;

/// <summary>
/// <see cref="OutboxPublisher"/> の単体テスト。
/// InMemory EF Core を使用して Outbox イベントの発行ロジックを検証する。
/// Kafka プロデューサーは NSubstitute でモック化し、DB 操作の正確性に集中する。
/// </summary>
[Trait("Category", "Unit")]
public class OutboxPublisherTests
{
    [Fact]
    public async Task Should_NotThrow_When_NoEventsExist()
    {
        // Arrange
        var (scopeFactory, producer, context) = CreateDependencies("empty-db");

        // Outbox にイベントなし（空 DB）

        var logger = Substitute.For<ILogger<OutboxPublisher>>();
        var sut = new OutboxPublisher(scopeFactory, producer, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act & Assert — 例外なく停止すること
        var act = async () =>
        {
            try
            {
                await sut.StartAsync(cts.Token);
                await Task.Delay(300, CancellationToken.None);
                await sut.StopAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // CancellationToken による正常停止は許容
            }
        };

        await Should.NotThrowAsync(act);
    }

    /// <summary>
    /// InMemory DbContext とモック Kafka プロデューサーを構築する。
    /// OutboxPublisher は IServiceScopeFactory 経由で DbContext を取得するため、
    /// ServiceProvider を構成して DI 動作を再現する。
    /// </summary>
    private static (IServiceScopeFactory scopeFactory, IProducer<string, string> producer, AppDbContext context)
        CreateDependencies(string dbName)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName));
        services.AddSingleton<TimeProvider>(timeProvider);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var context = serviceProvider.GetRequiredService<AppDbContext>();

        var producer = Substitute.For<IProducer<string, string>>();

        return (scopeFactory, producer, context);
    }
}
