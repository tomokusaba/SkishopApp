namespace InventoryManagementService.Exceptions;

/// <summary>
/// 在庫管理サービスの基底例外クラス。
/// 構造化されたエラーコードと詳細情報を保持し、グローバル例外ハンドラーで HTTP レスポンスにマッピングされる。
/// </summary>
/// <param name="errorCode">構造化エラーコード（例: "INV_001", "PROD_002"）。</param>
/// <param name="message">エラーメッセージ。</param>
/// <param name="details">エラーの追加詳細情報（リソース ID、要求数量など）。</param>
public class InventoryException(string errorCode, string message, Dictionary<string, object>? details = null)
    : Exception(message)
{
    /// <summary>構造化エラーコード。クライアント側でのエラー分類に使用する。</summary>
    public string ErrorCode { get; } = errorCode;

    /// <summary>エラーの追加コンテキスト情報。Problem Details の拡張プロパティとして返却される。</summary>
    public Dictionary<string, object> Details { get; } = details ?? [];
}
