using System.ComponentModel.DataAnnotations;

namespace InventoryManagementService.DTOs.Requests;

/// <summary>
/// カテゴリ新規作成リクエスト DTO。
/// POST /categories エンドポイントで使用する。
/// </summary>
/// <param name="Name">カテゴリ名（必須、最大 255 文字）</param>
/// <param name="Description">カテゴリの説明（任意、最大 2000 文字）</param>
/// <param name="ParentId">親カテゴリ ID（任意、階層構造を構成する場合に指定）</param>
public record CategoryCreateRequest(
    [Required, StringLength(255)]
    string Name,
    [StringLength(2000)]
    string? Description,
    string? ParentId);

/// <summary>
/// カテゴリ更新リクエスト DTO。
/// PUT /categories/{id} エンドポイントで使用する。
/// 指定されたフィールドのみ部分更新を行う。
/// </summary>
/// <param name="Name">カテゴリ名（任意、最大 255 文字）</param>
/// <param name="Description">カテゴリの説明（任意、最大 2000 文字）</param>
/// <param name="IsActive">有効/無効フラグ（任意）</param>
public record CategoryUpdateRequest(
    [StringLength(255)]
    string? Name,
    [StringLength(2000)]
    string? Description,
    bool? IsActive);
