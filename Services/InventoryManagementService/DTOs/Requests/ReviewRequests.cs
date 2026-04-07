using System.ComponentModel.DataAnnotations;

namespace InventoryManagementService.DTOs.Requests;

/// <summary>
/// レビュー新規作成リクエスト DTO。
/// POST /reviews エンドポイントで使用する。
/// ユーザーが商品に対してレビュー（評価・タイトル・本文）を投稿する。
/// </summary>
/// <param name="ProductId">対象商品 ID（必須）</param>
/// <param name="Rating">評価（1〜5 の整数）</param>
/// <param name="Title">レビュータイトル（必須、最大 255 文字）</param>
/// <param name="Content">レビュー本文（任意、最大 5000 文字）</param>
public record ReviewCreateRequest(
    [Required]
    string ProductId,
    [Range(1, 5, ErrorMessage = "評価は 1〜5 の整数を指定してください")]
    int Rating,
    [Required, StringLength(255)]
    string Title,
    [StringLength(5000)]
    string? Content);

/// <summary>
/// レビューステータス更新リクエスト DTO。
/// PUT /reviews/{id}/status エンドポイントで使用する（管理者専用）。
/// レビューを承認（APPROVED）または却下（REJECTED）する。
/// </summary>
/// <param name="Status">ステータス（"APPROVED" または "REJECTED"）</param>
public record ReviewStatusUpdateRequest(
    [Required, RegularExpression("^(APPROVED|REJECTED)$")]
    string Status);

/// <summary>
/// レビュー返信作成リクエスト DTO。
/// POST /reviews/{id}/responses エンドポイントで使用する。
/// 店舗スタッフ等がレビューに対して返信を投稿する。
/// </summary>
/// <param name="Content">返信内容（必須、最大 5000 文字）</param>
public record ReviewResponseCreateRequest(
    [Required, StringLength(5000)]
    string Content);
