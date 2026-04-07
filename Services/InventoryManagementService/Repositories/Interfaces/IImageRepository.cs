namespace InventoryManagementService.Repositories.Interfaces;

/// <summary>
/// 画像リポジトリのインターフェース。Azure Blob Storage を使用した画像ファイルの CRUD 操作を提供する。
/// </summary>
public interface IImageRepository
{
    /// <summary>
    /// 商品画像を Azure Blob Storage にアップロードする。products/{GUID}/{fileName} のパスで保存する。
    /// </summary>
    /// <param name="stream">画像データストリーム</param>
    /// <param name="fileName">ファイル名</param>
    /// <param name="contentType">Content-Type（例: image/jpeg）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>アップロードされた Blob の URL</returns>
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// サムネイル画像を Azure Blob Storage にアップロードする。products/thumbnails/{GUID}/{fileName} のパスで保存する。
    /// </summary>
    /// <param name="stream">サムネイルデータストリーム</param>
    /// <param name="fileName">ファイル名</param>
    /// <param name="contentType">Content-Type</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>アップロードされた Blob の URL</returns>
    Task<string> UploadThumbnailAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Azure Blob Storage から画像を削除する。Blob が存在しない場合はエラーにならない。
    /// </summary>
    /// <param name="blobName">Blob 名（パスを含む）</param>
    /// <param name="ct">キャンセルトークン</param>
    Task DeleteAsync(string blobName, CancellationToken ct = default);

    /// <summary>
    /// Azure Blob Storage から画像をダウンロードする。
    /// </summary>
    /// <param name="blobName">Blob 名（パスを含む）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>画像データのストリーム</returns>
    Task<Stream> DownloadAsync(string blobName, CancellationToken ct = default);
}
