using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using InventoryManagementService.Repositories.Interfaces;

namespace InventoryManagementService.Repositories;

/// <summary>
/// Azure Blob Storage を使用した画像リポジトリの実装クラス。
/// </summary>
/// <remarks>
/// BlobContainerClient を DI でインジェクションし、商品画像の CRUD 操作を提供する。
/// Blob 名は products/{GUID}/{fileName} 形式で、GUID により一意性を保証する。
/// </remarks>
public class ImageRepository(BlobContainerClient containerClient) : IImageRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// Blob パスは products/{GUID}/{fileName} 形式で、GUID により一意性を保証する。
    /// BlobHttpHeaders で Content-Type を設定し、ブラウザでの直接表示を可能にする。
    /// </remarks>
    public async Task<string> UploadAsync(
        Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var blobName = $"products/{Guid.NewGuid()}/{fileName}";
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType },
            cancellationToken: ct);

        return blobClient.Uri.ToString();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Blob パスは products/thumbnails/{GUID}/{fileName} 形式で、通常画像とは異なるディレクトリに格納する。
    /// サムネイルはリスト表示・検索結果で使用されるため、軽量な画像を想定する。
    /// </remarks>
    public async Task<string> UploadThumbnailAsync(
        Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var blobName = $"products/thumbnails/{Guid.NewGuid()}/{fileName}";
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType },
            cancellationToken: ct);

        return blobClient.Uri.ToString();
    }

    /// <inheritdoc />
    /// <remarks>
    /// DeleteIfExistsAsync を使用し、Blob が存在しない場合でも例外をスローしない（べき等操作）。
    /// </remarks>
    public async Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// DownloadStreamingAsync でストリーミングダウンロードを行い、メモリ効率を最適化する。
    /// 呼び出し側で返却された Stream を適切に Dispose すること。
    /// </remarks>
    public async Task<Stream> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        var blobClient = containerClient.GetBlobClient(blobName);
        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }
}
