using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuthService.Infrastructure.Persistence;

/// <summary>
/// EF Core マイグレーション生成用の Design-time Factory。
/// dotnet ef migrations add / update コマンド実行時に使用される。
/// </summary>
public class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
        
        // Design-time 用のダミー接続文字列（マイグレーション生成のみに使用）
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=authdb;Username=skishop;Password=skishop_dev_password");

        return new AuthDbContext(optionsBuilder.Options, TimeProvider.System);
    }
}
