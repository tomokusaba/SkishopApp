using System.ComponentModel.DataAnnotations;

namespace UserManagementService.Configurations;

/// <summary>
/// GDPR データエクスポート設定。AES-256 暗号化キーと出力ディレクトリを保持する。
/// </summary>
public record DataExportSettings
{
    [Required]
    public string EncryptionKeyBase64 { get; init; } = string.Empty;

    public string? ExportDirectory { get; init; }
}
