using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace UserManagementService.Infrastructure.Persistence;

/// <summary>
/// EF Core Migrations の設計時ファクトリ。<c>dotnet ef migrations add</c> 時に使用される。
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=userdb;Username=skishop;Password=skishop_dev_password");

        return new AppDbContext(
            optionsBuilder.Options,
            TimeProvider.System,
            NullLoggerFactory.Instance);
    }
}
