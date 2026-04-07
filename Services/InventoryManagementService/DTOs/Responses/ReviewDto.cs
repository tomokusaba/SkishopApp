namespace InventoryManagementService.DTOs.Responses;

/// <summary>
/// レビュー情報レスポンス DTO。
/// 商品レビュー API のレスポンスとして返却される。
/// レビュー本体の情報と、紐づく返信（Responses）のリストを含む。
/// </summary>
/// <param name="Id">レビュー ID</param>
/// <param name="ProductId">対象商品 ID</param>
/// <param name="UserId">投稿ユーザー ID</param>
/// <param name="Rating">評価（1〜5）</param>
/// <param name="Title">レビュータイトル</param>
/// <param name="Content">レビュー本文</param>
/// <param name="IsVerifiedPurchase">購入確認済みフラグ</param>
/// <param name="HelpfulCount">「参考になった」の件数</param>
/// <param name="Status">レビューステータス（PENDING/APPROVED/REJECTED）</param>
/// <param name="Responses">レビューへの返信リスト</param>
/// <param name="CreatedAt">投稿日時</param>
// M-12: sealed record 追加
public sealed record ReviewDto(
    string Id,
    string ProductId,
    string UserId,
    int Rating,
    string Title,
    string? Content,
    bool IsVerifiedPurchase,
    int HelpfulCount,
    string Status,
    List<ReviewResponseDto> Responses,
    DateTimeOffset CreatedAt);

/// <summary>
/// レビュー返信レスポンス DTO。
/// レビューに対する店舗スタッフ等からの返信情報を返す。
/// </summary>
/// <param name="Id">返信 ID</param>
/// <param name="ReviewId">対象レビュー ID</param>
/// <param name="ResponderId">返信者 ID</param>
/// <param name="Content">返信内容</param>
/// <param name="CreatedAt">返信日時</param>
// M-12: sealed record 追加
public sealed record ReviewResponseDto(
    string Id,
    string ReviewId,
    string ResponderId,
    string Content,
    DateTimeOffset CreatedAt);
