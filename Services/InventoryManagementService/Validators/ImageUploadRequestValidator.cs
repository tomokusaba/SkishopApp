using FluentValidation;

namespace InventoryManagementService.Validators;

/// <summary>
/// 画像アップロード（<see cref="IFormFile"/>）のバリデーター。
/// アップロードされたファイルのサイズ・形式・内容の整合性を検証する。
/// </summary>
/// <remarks>
/// 検証ルール:
/// <list type="bullet">
///   <item>ファイルサイズは 1 バイト以上 10MB 以下</item>
///   <item>Content-Type は image/jpeg, image/png, image/webp のいずれか</item>
///   <item>ファイルヘッダーのマジックバイトが宣言された Content-Type と一致すること（偽装防止）</item>
/// </list>
/// </remarks>
public class ImageUploadRequestValidator : AbstractValidator<IFormFile>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, byte[]> AllowedMagicBytes = new()
    {
        { "image/jpeg", [0xFF, 0xD8, 0xFF] },
        { "image/png", [0x89, 0x50, 0x4E, 0x47] },
        { "image/webp", [0x52, 0x49, 0x46, 0x46] }
    };

    public ImageUploadRequestValidator()
    {
        RuleFor(x => x.Length)
            .GreaterThan(0)
            .WithMessage("ファイルが空です")
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage("ファイルサイズは 10MB 以下にしてください");

        RuleFor(x => x.ContentType)
            .Must(ct => AllowedMagicBytes.ContainsKey(ct))
            .WithMessage("許可されているファイル形式は JPEG, PNG, WebP のみです");

        RuleFor(x => x)
            .Must(ValidateMagicBytes)
            .WithMessage("ファイルの内容が宣言された形式と一致しません");
    }

    private static bool ValidateMagicBytes(IFormFile file)
    {
        if (!AllowedMagicBytes.TryGetValue(file.ContentType, out var expectedBytes))
            return false;

        using var stream = file.OpenReadStream();
        var headerBytes = new byte[expectedBytes.Length];
        if (stream.Read(headerBytes, 0, expectedBytes.Length) < expectedBytes.Length)
            return false;

        return headerBytes.AsSpan().StartsWith(expectedBytes);
    }
}
