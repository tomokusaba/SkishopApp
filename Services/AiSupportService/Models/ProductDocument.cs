namespace AiSupportService.Models;

/// <summary>
/// ベクトル検索用の商品ドキュメントを表す Value Object。
/// </summary>
/// <remarks>
/// <para>
/// このレコードは商品情報とその埋め込みベクトル（Embedding）を組み合わせた
/// ベクトル検索用のドキュメントを表現します。
/// セマンティック検索や類似商品検索で使用されます。
/// </para>
/// <para>
/// 主な用途:
/// <list type="bullet">
///   <item><description>自然言語による商品検索（例: 「初心者向けの軽いスキー板」）</description></item>
///   <item><description>類似商品のレコメンデーション</description></item>
///   <item><description>チャットアシスタントが商品を参照する際のコンテキスト</description></item>
/// </list>
/// </para>
/// <para>
/// このレコードはデータベースに直接マッピングされず、
/// ベクトルデータベース（例: Qdrant, Pinecone）やメモリ内キャッシュに格納されます。
/// </para>
/// </remarks>
/// <param name="ProductId">
/// 商品の一意識別子。InventoryManagementService で管理される商品 ID を参照します。
/// </param>
/// <param name="Name">
/// 商品名。検索結果の表示やコンテキスト構築に使用します。
/// </param>
/// <param name="Description">
/// 商品の詳細説明。埋め込みベクトル生成の入力テキストとして使用されます。
/// </param>
/// <param name="Price">
/// 商品の価格（税込）。検索結果のフィルタリングや表示に使用します。
/// </param>
/// <param name="Category">
/// 商品カテゴリ（例: <c>"スキー板"</c>, <c>"ブーツ"</c>, <c>"ウェア"</c>）。
/// 検索結果のグルーピングやフィルタリングに使用します。
/// </param>
/// <param name="ImageUrl">
/// 商品画像の URL。チャット応答で商品を紹介する際に使用します。
/// </param>
/// <param name="Embedding">
/// 商品のベクトル表現（埋め込み）。
/// <para>
/// 商品名と説明文を AI モデル（例: OpenAI text-embedding-ada-002）で
/// ベクトル化したものです。通常 1536 次元の浮動小数点配列です。
/// </para>
/// <para>
/// コサイン類似度を用いてセマンティック検索を実現します。
/// </para>
/// </param>
/// <example>
/// <code>
/// var document = new ProductDocument(
///     ProductId: "prod-123",
///     Name: "アルペンスキー Pro 2024",
///     Description: "中上級者向けのオールラウンドスキー板。カービングと安定性を両立。",
///     Price: 89800m,
///     Category: "スキー板",
///     ImageUrl: "https://example.com/images/ski-pro-2024.jpg",
///     Embedding: embeddingVector // 1536次元の float[]
/// );
/// </code>
/// </example>
public record ProductDocument(
    string ProductId,
    string Name,
    string? Description,
    decimal Price,
    string? Category,
    string? ImageUrl,
    float[]? Embedding);
