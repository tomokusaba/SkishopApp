using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MailSendService.Infrastructure.Persistence;

/// <summary>
/// EF Core マイグレーション生成用のデザイン時ファクトリ。
/// <c>dotnet ef migrations add</c> コマンドで使用される。
/// </summary>
/// <remarks>
/// 実行時の DI 設定（Program.cs）とは独立して DbContext を構成するため、
/// マイグレーション生成時にアプリケーション全体の起動が不要になる。
/// P2-6: フォールバック接続文字列を削除し、環境変数必須に変更。
/// </remarks>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// マイグレーション生成用の <see cref="AppDbContext"/> を構成する。
    /// </summary>
    /// <param name="args">コマンドライン引数。</param>
    /// <returns>設定済みの <see cref="AppDbContext"/> インスタンス。</returns>
    /// <exception cref="InvalidOperationException">環境変数 DESIGN_TIME_CONNECTION が設定されていない場合。</exception>
    /// <remarks>
    /// P2-6: 秘密情報のハードコードを防止するため、接続文字列は環境変数から取得する。
    /// 環境変数が設定されていない場合は明示的なエラーを発生させる。
    /// </remarks>
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DESIGN_TIME_CONNECTION")
            ?? throw new InvalidOperationException(
                "環境変数 DESIGN_TIME_CONNECTION が設定されていません。" +
                "マイグレーション生成前に以下を実行してください: " +
                "export DESIGN_TIME_CONNECTION='Host=localhost;Database=skishop_mail;Username=postgres;Password=...'");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new AppDbContext(optionsBuilder.Options, TimeProvider.System);
    }
}
