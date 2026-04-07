using InventoryManagementService.Infrastructure.Persistence;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace InventoryManagementService.Tests.Services;

/// <summary>
/// <see cref="InventoryRepository"/> の DB スライステスト。
/// Testcontainers.PostgreSql を使用して実際の PostgreSQL コンテナ上で
/// 在庫検索クエリの正確性を検証する。
/// </summary>
[Trait("Category", "Integration")]
public class InventoryRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("inventory_repo_test")
        .Build();

    private AppDbContext _context = null!;
    private InventoryRepository _sut = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _context = new AppDbContext(options, timeProvider);
        await _context.Database.EnsureCreatedAsync();

        _sut = new InventoryRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Should_FindInventory_When_ProductIdExists()
    {
        // Arrange
        var category = new Category { Id = "cat-inv-1", Name = "Gear" };
        var product = new Product
        {
            Id = "prod-inv-1",
            Sku = "SKI-INV-001",
            Name = "Ski Pole",
            CategoryId = category.Id,
            IsActive = true
        };
        var inventory = new Inventory
        {
            Id = "inv-1",
            ProductId = "prod-inv-1",
            Quantity = 100,
            ReservedQuantity = 0,
            LocationCode = "WH-01",
            Status = "IN_STOCK",
            ReorderPoint = 10
        };

        _context.Categories.Add(category);
        _context.Products.Add(product);
        _context.Inventories.Add(inventory);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.FindByProductIdAsync("prod-inv-1");

        // Assert
        result.ShouldNotBeNull();
        result.ProductId.ShouldBe("prod-inv-1");
        result.Quantity.ShouldBe(100);
        result.LocationCode.ShouldBe("WH-01");
    }

    [Fact]
    public async Task Should_ReturnNull_When_ProductIdDoesNotExist()
    {
        // Act
        var result = await _sut.FindByProductIdAsync("nonexistent-product");

        // Assert
        result.ShouldBeNull();
    }
}
