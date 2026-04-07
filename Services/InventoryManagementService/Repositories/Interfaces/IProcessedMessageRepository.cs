using InventoryManagementService.Models;

namespace InventoryManagementService.Repositories.Interfaces;

/// <summary>
/// ProcessedMessage エンティティのリポジトリインターフェース。
/// メッセージの冪等性保証のための処理済みメッセージ管理を担当する。
/// </summary>
public interface IProcessedMessageRepository
{
    /// <summary>
    /// 指定されたメッセージ ID が既に処理済みかどうかを確認する。
    /// </summary>
    /// <param name="messageId">メッセージ ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>処理済みの場合 true</returns>
    Task<bool> ExistsAsync(string messageId, CancellationToken ct = default);

    /// <summary>
    /// ProcessedMessage を追加する。
    /// </summary>
    /// <param name="message">追加する処理済みメッセージ</param>
    /// <param name="ct">キャンセルトークン</param>
    Task AddAsync(ProcessedMessage message, CancellationToken ct = default);

    /// <summary>
    /// 変更を永続化する。
    /// </summary>
    /// <param name="ct">キャンセルトークン</param>
    Task SaveChangesAsync(CancellationToken ct = default);
}
