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
/// <see cref="ProductRepository"/> の DB スライステスト。
/// Testcontainers.PostgreSql を使用して実際の PostgreSQL コンテナ上で
/// CRUD 操作と検索クエリの正確性を検証する。
/// </summary>
[Trait("Category", "Integration")]
public class ProductRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("product_repo_test")
        .Build();

    private AppDbContext _context = null!;
    private ProductRepository _sut = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _context = new AppDbContext(options, timeProvider);
        await _context.Database.EnsureCreatedAsync();

        _sut = new ProductRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Should_FindProduct_When_IdExists()
    {
        // Arrange
        var category = new Category { Id = "cat-1", Name = "Ski Boots" };
        var product = new Product
        {
            Id = "prod-find-1",
            Sku = "SKI-FIND-001",
            Name = "Test Ski Boot",
            Description = "A test product",
            Brand = "TestBrand",
            CategoryId = category.Id,
            IsActive = true
        };

        _context.Categories.Add(category);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.FindByIdAsync("prod-find-1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("prod-find-1");
        result.Sku.ShouldBe("SKI-FIND-001");
        result.Name.ShouldBe("Test Ski Boot");
    }

    [Fact]
    public async Task Should_ReturnNull_When_IdDoesNotExist()
    {
        // Act
        var result = await _sut.FindByIdAsync("nonexistent-id");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Should_AddProduct_Successfully()
    {
        // Arrange
        var category = new Category { Id = "cat-add-1", Name = "Skis" };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var product = new Product
        {
            Id = "prod-add-1",
            Sku = "SKI-ADD-001",
            Name = "New Ski Product",
            Description = "Newly added product",
            Brand = "NewBrand",
            CategoryId = category.Id,
            IsActive = true
        };

        // Act
        await _sut.AddAsync(product);
        await _sut.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Assert
        var saved = await _context.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == "prod-add-1");
        saved.ShouldNotBeNull();
        saved.Sku.ShouldBe("SKI-ADD-001");
        saved.Name.ShouldBe("New Ski Product");
    }
}
