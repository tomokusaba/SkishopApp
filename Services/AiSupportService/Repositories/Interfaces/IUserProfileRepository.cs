using AiSupportService.Models;

namespace AiSupportService.Repositories.Interfaces;

/// <summary>
/// Repository for managing UserProfile entity persistence.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Aggregate Root:</strong> UserProfile
/// </para>
/// <para>
/// このリポジトリは AI サービス用のユーザープロファイルを管理します。
/// ユーザーの嗜好、行動履歴サマリー、パーソナライゼーション設定など、
/// AI 機能のカスタマイズに必要な情報を永続化します。
/// </para>
/// <para>
/// <strong>主な責務:</strong>
/// <list type="bullet">
///   <item><description>ユーザープロファイルの CRUD 操作</description></item>
///   <item><description>プロファイルの自動作成（GetOrCreate パターン）</description></item>
///   <item><description>読み取り専用/更新用の取得メソッド分離</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>プロファイルの用途:</strong>
/// <list type="bullet">
///   <item><description>レコメンデーションのパーソナライズ</description></item>
///   <item><description>チャットボットの応答カスタマイズ</description></item>
///   <item><description>検索結果のパーソナライズ</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IUserProfileRepository
{
    /// <summary>
    /// 指定されたユーザー ID のプロファイルを読み取り専用で取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// プロファイル情報の参照に使用されます。
    /// 変更追跡なしで取得するため、パフォーマンスに優れています。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>存在しないユーザー ID を指定した場合は null を返す</description></item>
    ///   <item><description>読み取り専用クエリとして実行される（AsNoTracking）</description></item>
    ///   <item><description>取得したプロファイルを変更しても DB には反映されない</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="userId">ユーザー ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>見つかった場合はプロファイル、見つからない場合は null。</returns>
    Task<UserProfile?> FindByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// 指定されたユーザー ID のプロファイルを更新用（変更追跡付き）で取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// プロファイル情報の更新に使用されます。
    /// 変更追跡が有効な状態で取得するため、プロパティ変更後に
    /// <see cref="SaveChangesAsync"/> を呼び出すと DB に反映されます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>存在しないユーザー ID を指定した場合は null を返す</description></item>
    ///   <item><description>変更追跡が有効（更新可能な状態で取得）</description></item>
    ///   <item><description>プロパティ変更後、SaveChangesAsync で DB に反映される</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="userId">ユーザー ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>見つかった場合はプロファイル、見つからない場合は null。</returns>
    Task<UserProfile?> FindByUserIdForUpdateAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// 指定されたユーザー ID のプロファイルを取得し、存在しない場合は新規作成する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// プロファイルの存在を保証したい場合に使用されます。
    /// 初回アクセス時に自動的にデフォルトプロファイルを作成します。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>既存プロファイルがある場合はそれを返す</description></item>
    ///   <item><description>存在しない場合はデフォルト値で新規作成して返す</description></item>
    ///   <item><description>新規作成時は自動的に SaveChangesAsync が呼ばれる</description></item>
    ///   <item><description>一意制約違反（同時作成競合）を適切にハンドリングする</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>同時実行の考慮:</strong>
    /// 複数のリクエストが同時に同一ユーザーの初回アクセスを行った場合、
    /// 一意制約違反をキャッチして既存レコードを取得し直します。
    /// </para>
    /// </remarks>
    /// <param name="userId">ユーザー ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>既存または新規作成されたプロファイル。</returns>
    Task<UserProfile> GetOrCreateAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// 新しいユーザープロファイルを追加する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 明示的にプロファイルを作成する場合に使用されます。
    /// 追加されたプロファイルは <see cref="SaveChangesAsync"/> を呼び出すまで永続化されません。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>プロファイルを DbContext に追加する（未コミット状態）</description></item>
    ///   <item><description>Id は事前に設定されている必要がある</description></item>
    ///   <item><description>UserId が重複する場合、SaveChangesAsync 時に例外が発生する</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>注意:</strong> 通常は <see cref="GetOrCreateAsync"/> の使用を推奨します。
    /// </para>
    /// </remarks>
    /// <param name="profile">追加するユーザープロファイル。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task AddAsync(UserProfile profile, CancellationToken ct = default);

    /// <summary>
    /// 保留中の変更をデータベースに保存する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unit of Work パターンに基づき、追跡中の全ての変更を
    /// 単一のトランザクションとしてデータベースにコミットします。
    /// </para>
    /// </remarks>
    /// <param name="ct">キャンセルトークン。</param>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">データベース更新時にエラーが発生した場合。</exception>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException">楽観的ロック競合が発生した場合。</exception>
    Task SaveChangesAsync(CancellationToken ct = default);
}
