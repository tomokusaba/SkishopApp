using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UserManagementService.Configurations;
using UserManagementService.Repositories.Interfaces;

namespace UserManagementService.BackgroundServices;

/// <summary>
/// GDPR データエクスポートの BackgroundService。5 分間隔でペンディングのエクスポート要求を処理する。
/// ユーザーの全関連データを JSON 化し、AES-GCM で暗号化してファイルに保存する。
/// </summary>
public class DataExportService(
    IServiceScopeFactory scopeFactory,
    IOptions<DataExportSettings> exportOptions,
    TimeProvider timeProvider,
    ILogger<DataExportService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dataExportRepository = scope.ServiceProvider.GetRequiredService<IDataExportRepository>();

                var pendingUserIds = await dataExportRepository.FindPendingExportUserIdsAsync(10, stoppingToken);

                foreach (var userId in pendingUserIds)
                {
                    var exportData = await GenerateExportDataAsync(
                        dataExportRepository,
                        userId,
                        timeProvider.GetUtcNow(),
                        stoppingToken);
                    var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(exportData);
                    var encryptedData = EncryptData(jsonBytes);

                    // パストラバーサル防止: userId をファイル名安全な文字列に正規化
                    var safeUserId = Path.GetFileName(userId);
                    if (string.IsNullOrWhiteSpace(safeUserId) || safeUserId != userId)
                        throw new InvalidOperationException($"無効な UserId: {userId}");

                    var exportDir = Path.Combine(
                        exportOptions.Value.ExportDirectory ?? "/tmp/exports",
                        safeUserId);
                    Directory.CreateDirectory(exportDir);
                    var fileName = $"export_{timeProvider.GetUtcNow():yyyyMMdd_HHmmss}.enc";
                    var filePath = Path.Combine(exportDir, fileName);
                    await File.WriteAllBytesAsync(filePath, encryptedData, stoppingToken);

                    await dataExportRepository.MarkExportCompletedAsync(userId, stoppingToken);

                    logger.LogInformation("データエクスポート完了: {UserId}, Path={Path}, EncryptedSize={Size}",
                        userId, filePath, encryptedData.Length);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "データエクスポート処理でエラーが発生しました: {Message}", ex.Message);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private static async Task<object> GenerateExportDataAsync(
        IDataExportRepository dataExportRepository,
        string userId,
        DateTimeOffset exportedAt,
        CancellationToken ct)
    {
        var user = await dataExportRepository.FindUserExportDataAsync(userId, ct);

        return new
        {
            ExportedAt = exportedAt,
            User = user is not null ? new { user.Id, user.FirstName, user.LastName, user.PhoneNumber, user.BirthDate, user.CreatedAt } : null,
            Addresses = user?.Addresses.Select(a => new { a.Id, a.AddressType, a.Recipient, a.Prefecture, a.City, a.StreetAddress }) ?? [],
            Activities = user?.Activities.Select(a => new { a.Id, a.ActivityType, a.Details, a.Timestamp }) ?? [],
            Consents = user?.Consents.Select(c => new { c.Id, c.ConsentType, c.IsGranted, c.UpdatedAt }) ?? [],
            Preferences = user?.Preference is { } p ? new { p.Language, p.Currency } : null
        };
    }

    /// <summary>エクスポートデータを AES-GCM で暗号化する。Nonce(12) + Tag(16) + CipherText の順で連結して返す。</summary>
    private byte[] EncryptData(byte[] data)
    {
        var key = Convert.FromBase64String(exportOptions.Value.EncryptionKeyBase64);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];     // 16 bytes
        var ciphertext = new byte[data.Length];

        using var aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aesGcm.Encrypt(nonce, data, ciphertext, tag);

        // Nonce(12) + Tag(16) + CipherText を連結
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);
        return result;
    }
}
