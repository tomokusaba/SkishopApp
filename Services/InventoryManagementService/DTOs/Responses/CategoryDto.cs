namespace InventoryManagementService.DTOs.Responses;

/// <summary>
/// カテゴリ情報レスポンス DTO。
/// カテゴリ API のレスポンスとして返却される。
/// 階層構造（Level・Path）を含むカテゴリデータを表す。
/// </summary>
/// <param name="Id">カテゴリ ID</param>
/// <param name="Name">カテゴリ名</param>
/// <param name="Description">カテゴリの説明</param>
/// <param name="ParentId">親カテゴリ ID（ルートカテゴリの場合は null）</param>
/// <param name="Level">階層レベル（ルートが 0）</param>
/// <param name="Path">階層パス（例: "スキー/ブーツ"）</param>
/// <param name="IsActive">有効/無効フラグ</param>
// M-8: sealed record 追加
public sealed record CategoryDto(
    string Id,
    string Name,
    string? Description,
    string? ParentId,
    int Level,
    string? Path,
    bool IsActive);
