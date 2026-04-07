using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SalesManagementService.Infrastructure.Persistence;

/// <summary>
/// EF Core マイグレーションツール用の DbContext ファクトリ。
/// dotnet ef migrations add / update 時に使用される。
/// </summary>
public class SalesDbContextFactory : IDesignTimeDbContextFactory<SalesDbContext>
{
    public SalesDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SalesDbContext>();

        // デザイン時用の接続文字列（環境変数からも取得可能）
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__salesdb")
            ?? "Host=localhost;Database=salesdb;Username=skishop;Password=skishop_dev_password";

        optionsBuilder.UseNpgsql(connectionString);

        return new SalesDbContext(optionsBuilder.Options, TimeProvider.System);
    }
}
