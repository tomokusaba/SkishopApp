using System.Security.Cryptography;
using System.Text;
using AuthService.Services.Interfaces;

namespace AuthService.Services;

/// <summary>
/// <see cref="ITotpService"/> の実装クラス。
/// RFC 6238準拠のTOTP（Time-based One-Time Password）の生成と検証を提供します。
/// </summary>
/// <remarks>
/// 実装詳細:
/// <list type="bullet">
///   <item>HMAC-SHA1アルゴリズムを使用（RFC 6238標準）</item>
///   <item>30秒の時間ステップ</item>
///   <item>6桁のOTPコード</item>
///   <item>Base32エンコーディングでシークレットを表現</item>
///   <item>前後1ステップ（30秒）の時間ずれを許容</item>
/// </list>
/// </remarks>
/// <param name="timeProvider">時刻プロバイダー。</param>
/// <param name="logger">ロガー。</param>
public class TotpService(TimeProvider timeProvider, ILogger<TotpService> logger) : ITotpService
{
    /// <summary>
    /// TOTPの時間ステップ（秒）。RFC 6238推奨値。
    /// </summary>
    private const int TimeStep = 30;

    /// <summary>
    /// 生成されるOTPコードの桁数。
    /// </summary>
    private const int CodeDigits = 6;

    /// <summary>
    /// Base32エンコーディングに使用する文字セット。
    /// </summary>
    private const string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <inheritdoc />
    public Task<string> GenerateSecretAsync(CancellationToken ct = default)
    {
        var secretBytes = RandomNumberGenerator.GetBytes(20);
        var base32Secret = ToBase32(secretBytes);

        logger.LogInformation("TOTP シークレット生成完了");

        return Task.FromResult(base32Secret);
    }

    /// <inheritdoc />
    public string GenerateQrCodeUri(string email, string secret)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(secret);

        var encodedEmail = Uri.EscapeDataString(email);
        var issuer = Uri.EscapeDataString("SkiShop");
        return $"otpauth://totp/{issuer}:{encodedEmail}?secret={secret}&issuer={issuer}&digits={CodeDigits}&period={TimeStep}";
    }

    /// <inheritdoc />
    public bool VerifyCode(string secret, string code)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(code);

        if (code.Length != CodeDigits) return false;

        var secretBytes = FromBase32(secret);
        var unixTimestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        for (var offset = -1; offset <= 1; offset++)
        {
            var timeCounter = (unixTimestamp / TimeStep) + offset;
            var expectedCode = ComputeTotp(secretBytes, timeCounter);
            if (string.Equals(expectedCode, code, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GenerateBackupCodes(int count = 8)
    {
        var codes = new List<string>(count);
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        for (var i = 0; i < count; i++)
        {
            var codeBytes = RandomNumberGenerator.GetBytes(8);
            var sb = new StringBuilder(8);
            foreach (var b in codeBytes)
            {
                sb.Append(chars[b % chars.Length]);
            }
            codes.Add(sb.ToString());
        }

        return codes;
    }

    /// <summary>
    /// 指定された時間カウンターに基づいてTOTPコードを計算します。
    /// </summary>
    /// <param name="secret">シークレットキー（バイト配列）。</param>
    /// <param name="timeCounter">UNIXタイムスタンプを時間ステップで割った値。</param>
    /// <returns>計算された6桁のTOTPコード。</returns>
    private static string ComputeTotp(byte[] secret, long timeCounter)
    {
        var timeBytes = BitConverter.GetBytes(timeCounter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timeBytes);
        }

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(timeBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var otp = binaryCode % (int)Math.Pow(10, CodeDigits);
        return otp.ToString().PadLeft(CodeDigits, '0');
    }

    /// <summary>
    /// バイト配列をBase32文字列にエンコードします。
    /// </summary>
    /// <param name="data">エンコードするバイト配列。</param>
    /// <returns>Base32エンコードされた文字列。</returns>
    private static string ToBase32(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Chars[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
        {
            sb.Append(Base32Chars[(buffer << (5 - bitsLeft)) & 0x1F]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Base32文字列をバイト配列にデコードします。
    /// </summary>
    /// <param name="base32">デコードするBase32文字列。</param>
    /// <returns>デコードされたバイト配列。</returns>
    private static byte[] FromBase32(string base32)
    {
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in base32.ToUpperInvariant())
        {
            var index = Base32Chars.IndexOf(c);
            if (index < 0) continue;

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)(buffer >> bitsLeft));
            }
        }

        return output.ToArray();
    }
}
