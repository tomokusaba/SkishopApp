using AiSupportService.Infrastructure.Persistence;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiSupportService.Repositories;

/// <summary>
/// <see cref="IUserProfileRepository"/> の EF Core 実装。
/// </summary>
/// <remarks>
/// <para>
/// <strong>データベース操作:</strong>
/// PostgreSQL の user_profiles テーブルに対する CRUD 操作を提供します。
/// AI サービス用のユーザープロファイルを管理し、
/// パーソナライゼーション機能を支援します。
/// </para>
/// <para>
/// <strong>パフォーマンス考慮:</strong>
/// <list type="bullet">
///   <item><description>FindByUserIdAsync は AsNoTracking で読み取り専用最適化</description></item>
///   <item><description>FindByUserIdForUpdateAsync は変更追跡を有効化して更新に対応</description></item>
///   <item><description>user_id に UNIQUE インデックスを前提とした設計</description></item>
///   <item><description>GetOrCreateAsync で同時実行競合を安全にハンドリング</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>エラーハンドリング:</strong>
/// <list type="bullet">
///   <item><description>一般的な DbException はそのまま上位に伝搬</description></item>
///   <item><description>GetOrCreateAsync は一意制約違反（23505）をキャッチして既存レコードを返却</description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="context">EF Core DbContext。</param>
public class UserProfileRepository(AppDbContext context) : IUserProfileRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// user_profiles テーブルから user_id で検索します。
    /// AsNoTracking を使用し、読み取り専用クエリとして最適化されています。
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// 変更追跡が無効なため、取得後にプロパティを変更しても DB には反映されません。
    /// 参照のみの用途に使用してください。
    /// </para>
    /// </remarks>
    public async Task<UserProfile?> FindByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// user_profiles テーブルから user_id で検索します。
    /// 変更追跡が有効なため、取得後のプロパティ変更が SaveChangesAsync で反映されます。
    /// </para>
    /// </remarks>
    public async Task<UserProfile?> FindByUserIdForUpdateAsync(string userId, CancellationToken ct = default)
        => await context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// user_profiles テーブルから user_id で検索し、存在しない場合は新規レコードを作成します。
    /// 2 つのクエリ（SELECT + INSERT）を実行する可能性があります。
    /// </para>
    /// <para>
    /// <strong>同時実行の考慮:</strong>
    /// 複数のリクエストが同時に同一ユーザーの初回アクセスを行った場合、
    /// 一方が INSERT を完了した後、他方が INSERT を試みると一意制約違反が発生します。
    /// この場合、PostgreSQL エラーコード 23505（unique_violation）をキャッチし、
    /// エンティティを Detach した後、既存レコードを再取得します。
    /// </para>
    /// <para>
    /// <strong>エラーハンドリング:</strong>
    /// <list type="bullet">
    ///   <item><description>正常パス: SELECT → null → INSERT → SaveChanges → return</description></item>
    ///   <item><description>競合パス: SELECT → null → INSERT → SaveChanges（23505）→ Detach → SELECT → return</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task<UserProfile> GetOrCreateAsync(string userId, CancellationToken ct = default)
    {
        var profile = await context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (profile is not null)
            return profile;

        try
        {
            profile = new UserProfile { UserId = userId };
            await context.UserProfiles.AddAsync(profile, ct);
            await context.SaveChangesAsync(ct);
            return profile;
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // 同時実行による一意制約違反: 追跡から外して既存レコードを再取得
            context.Entry(profile!).State = EntityState.Detached;
            return await context.UserProfiles
                .FirstAsync(p => p.UserId == userId, ct);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// DbSet.AddAsync を使用して新しいプロファイルを追跡対象に追加します。
    /// 実際の INSERT は SaveChangesAsync 呼び出し時に実行されます。
    /// </para>
    /// <para>
    /// <strong>注意:</strong>
    /// UserId が重複する場合、SaveChangesAsync 時に DbUpdateException が発生します。
    /// 通常は GetOrCreateAsync の使用を推奨します。
    /// </para>
    /// </remarks>
    public async Task AddAsync(UserProfile profile, CancellationToken ct = default)
        => await context.UserProfiles.AddAsync(profile, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// DbContext.SaveChangesAsync を呼び出し、追跡中の全ての変更を
    /// 単一のトランザクションでデータベースにコミットします。
    /// </para>
    /// </remarks>
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
