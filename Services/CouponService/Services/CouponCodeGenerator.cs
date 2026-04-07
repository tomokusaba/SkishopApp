using System.Security.Cryptography;
using CouponService.Repositories.Interfaces;
using CouponService.Services.Interfaces;

namespace CouponService.Services;

public class CouponCodeGenerator(
    ICouponRepository couponRepository,
    ILogger<CouponCodeGenerator> logger) : ICouponCodeGenerator
{
    private const int CodeLength = 12;
    private const int MaxRetries = 10;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var code = GenerateRandomCode();
            var exists = await couponRepository.ExistsAsync(code, ct);
            if (!exists)
                return code;

            logger.LogWarning(
                "クーポンコード衝突: {Code}, リトライ: {Attempt}/{MaxRetries}",
                code, attempt + 1, MaxRetries);
        }

        throw new InvalidOperationException(
            $"クーポンコードの一意生成に {MaxRetries} 回失敗しました");
    }

    public async Task<List<string>> GenerateBatchAsync(int count, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var codes = new HashSet<string>(count);
        var maxAttempts = count * MaxRetries;

        for (var attempt = 0; codes.Count < count && attempt < maxAttempts; attempt++)
            codes.Add(GenerateRandomCode());

        // 一括存在チェック
        var existingCodes = await couponRepository.FindExistingCodesAsync(codes, ct);
        codes.ExceptWith(existingCodes);

        // 不足分を追加生成
        var remaining = count - codes.Count;
        for (var i = 0; i < remaining * MaxRetries && codes.Count < count; i++)
        {
            var code = GenerateRandomCode();
            if (!existingCodes.Contains(code))
                codes.Add(code);
        }

        if (codes.Count < count)
            throw new InvalidOperationException(
                $"一意なクーポンコードの生成に失敗しました（要求: {count}, 生成: {codes.Count}）");

        return codes.ToList();
    }

    private static string GenerateRandomCode()
    {
        Span<char> code = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return $"{code[..4]}-{code[4..8]}-{code[8..12]}";
    }
}
