namespace AuthService.Infrastructure.Persistence;

/// <summary>
/// 作成日時と更新日時のタイムスタンプを持つエンティティを示すマーカーインターフェース。
/// </summary>
/// <remarks>
/// <para>
/// このインターフェースを実装するエンティティは、<see cref="AuthDbContext.SaveChangesAsync"/> で
/// タイムスタンプが自動的に設定されます。
/// </para>
/// <para>
/// <strong>自動設定ルール:</strong>
/// <list type="bullet">
///   <item>
///     <term>新規エンティティ（Added）</term>
///     <description><see cref="CreatedAt"/> と <see cref="UpdatedAt"/> の両方が現在時刻に設定されます</description>
///   </item>
///   <item>
///     <term>更新エンティティ（Modified）</term>
///     <description><see cref="UpdatedAt"/> のみが現在時刻に設定されます</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// <strong>使用例:</strong>
/// <code>
/// public class User : IHasTimestamps
/// {
///     public DateTimeOffset CreatedAt { get; set; }
///     public DateTimeOffset UpdatedAt { get; set; }
///     // その他のプロパティ...
/// }
/// </code>
/// </para>
/// <para>
/// <strong>注意:</strong>
/// タイムスタンプは <see cref="TimeProvider"/> を使用して設定されるため、
/// テスト時には <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/> を
/// 使用して時刻を制御できます。
/// </para>
/// </remarks>
public interface IHasTimestamps
{
    /// <summary>
    /// エンティティの作成日時。
    /// </summary>
    /// <remarks>
    /// <para>
    /// エンティティが最初にデータベースに保存された日時を示します。
    /// この値は一度設定されると変更されません。
    /// </para>
    /// </remarks>
    DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// エンティティの最終更新日時。
    /// </summary>
    /// <remarks>
    /// <para>
    /// エンティティが最後に更新された日時を示します。
    /// エンティティが更新されるたびにこの値が更新されます。
    /// </para>
    /// </remarks>
    DateTimeOffset UpdatedAt { get; set; }
}
