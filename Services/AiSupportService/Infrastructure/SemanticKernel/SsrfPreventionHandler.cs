using System.Net;
using System.Net.Sockets;
using AiSupportService.Configurations;
using Microsoft.Extensions.Options;

namespace AiSupportService.Infrastructure.SemanticKernel;

/// <summary>
/// SSRF（Server-Side Request Forgery）攻撃を防止する HTTP メッセージハンドラー。
/// </summary>
/// <remarks>
/// <para>
/// <b>セキュリティ目的:</b>
/// OWASP Top 10 A10（Server-Side Request Forgery）対策として、
/// AI プラグインが外部 HTTP リクエストを送信する際に、内部ネットワークへのアクセスをブロックする。
/// </para>
/// <para>
/// <b>防御対象:</b>
/// <list type="bullet">
///   <item><description><b>ループバックアドレス:</b> <c>localhost</c>, <c>127.0.0.1</c>, <c>::1</c></description></item>
///   <item><description><b>プライベート IPv4:</b> <c>10.0.0.0/8</c>, <c>172.16.0.0/12</c>, <c>192.168.0.0/16</c></description></item>
///   <item><description><b>リンクローカル:</b> <c>169.254.0.0/16</c>, <c>fe80::/10</c></description></item>
///   <item><description><b>IPv6 ULA:</b> <c>fc00::/7</c></description></item>
///   <item><description><b>IPv6-mapped IPv4:</b> 内部 IPv4 にマッピングされた <c>::ffff:x.x.x.x</c></description></item>
/// </list>
/// </para>
/// <para>
/// <b>ホワイトリスト:</b>
/// <see cref="ServiceEndpointSettings"/> に登録された既知サービスエンドポイント
/// （InventoryManagementService, SalesManagementService 等）は、
/// 内部アドレスであっても許可される。
/// </para>
/// <para>
/// <b>DNS リバインディング対策:</b>
/// ホスト名を DNS 解決した後の IP アドレスもプライベートアドレスでないことを検証する。
/// これにより、DNS レコードを操作した攻撃を防止する。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs での HttpClient 登録
/// builder.Services.AddTransient&lt;SsrfPreventionHandler&gt;();
/// builder.Services.AddHttpClient("ExternalApi")
///     .AddHttpMessageHandler&lt;SsrfPreventionHandler&gt;();
/// </code>
/// </example>
/// <param name="serviceEndpoints">
/// ホワイトリスト対象のサービスエンドポイント設定。
/// </param>
/// <param name="logger">診断ログの出力先ロガー。セキュリティイベントの監査に使用。</param>
public class SsrfPreventionHandler(
    IOptions<ServiceEndpointSettings> serviceEndpoints,
    ILogger<SsrfPreventionHandler> logger) : DelegatingHandler
{
    private readonly ServiceEndpointSettings _endpoints = serviceEndpoints.Value;

    /// <summary>
    /// HTTP リクエスト送信前に SSRF チェックを実施し、安全な場合のみリクエストを転送する。
    /// </summary>
    /// <param name="request">送信対象の HTTP リクエストメッセージ。</param>
    /// <param name="cancellationToken">キャンセルトークン。DNS 解決にも適用される。</param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><description>安全な場合: 下位ハンドラーからの HTTP レスポンス</description></item>
    ///   <item><description>ブロック時: <see cref="HttpStatusCode.Forbidden"/> (403) レスポンス</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>チェックフロー:</b>
    /// <list type="number">
    ///   <item><description>URI null チェック → ブロック</description></item>
    ///   <item><description>ホワイトリスト（既知サービス）チェック → 許可</description></item>
    ///   <item><description>ホスト名の明示的ブロックチェック（localhost 等）→ ブロック</description></item>
    ///   <item><description>DNS 解決 → 解決後の IP がプライベートでないことを確認</description></item>
    ///   <item><description>全チェック通過 → リクエスト転送</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>ログ出力:</b> ブロック時は Warning レベルでログ出力する。
    /// セキュリティ監査のため、ブロックされたホスト名を記録する（IP アドレスは記録しない）。
    /// </para>
    /// </remarks>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            logger.LogWarning("SSRF 防止: URI が null のリクエストをブロック");
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("リクエスト URI が指定されていません")
            };
        }

        var uri = request.RequestUri;

        // ホワイトリスト: 既知サービスエンドポイントは許可
        if (IsAllowedServiceEndpoint(uri))
            return await base.SendAsync(request, cancellationToken);

        // ホスト名を DNS 解決して内部 IP をブロック
        if (IsBlockedHost(uri.Host))
        {
            logger.LogWarning("SSRF 防止: 内部ネットワークアドレスへのリクエストをブロック: {Host}", uri.Host);
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("内部ネットワークアドレスへのアクセスは禁止されています")
            };
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
            foreach (var addr in addresses)
            {
                if (IsPrivateOrLoopback(addr))
                {
                    logger.LogWarning("SSRF 防止: DNS 解決後の内部 IP をブロック: Host={Host}, IP={IpAddress}", uri.Host, addr);
                    return new HttpResponseMessage(HttpStatusCode.Forbidden)
                    {
                        Content = new StringContent("内部ネットワークアドレスへのアクセスは禁止されています")
                    };
                }
            }
        }
        catch (SocketException ex)
        {
            logger.LogWarning(ex, "SSRF 防止: DNS 解決失敗: {Host}", uri.Host);
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("ホスト名を解決できません")
            };
        }

        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// 指定された URI がホワイトリスト登録済みのサービスエンドポイントかどうかを判定する。
    /// </summary>
    /// <param name="uri">チェック対象の URI。</param>
    /// <returns>ホワイトリスト登録済みの場合は <c>true</c>。</returns>
    /// <remarks>
    /// Authority 部分（スキーム + ホスト + ポート）のみで比較する。
    /// パスやクエリ文字列は比較対象外。
    /// </remarks>
    private bool IsAllowedServiceEndpoint(Uri uri)
    {
        var uriString = uri.GetLeftPart(UriPartial.Authority);
        return uriString.Equals(new Uri(_endpoints.InventoryManagementService).GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)
            || uriString.Equals(new Uri(_endpoints.SalesManagementService).GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ホスト名が明示的にブロック対象（localhost 等）かどうかを判定する。
    /// </summary>
    /// <param name="host">チェック対象のホスト名。</param>
    /// <returns>明示的なブロック対象の場合は <c>true</c>。</returns>
    /// <remarks>
    /// DNS 解決前のホスト名レベルでのチェック。
    /// よく知られたローカルホスト名・IP アドレスを事前にブロックする。
    /// </remarks>
    private static bool IsBlockedHost(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host is "127.0.0.1" or "[::1]" or "0.0.0.0" or "[::ffff:127.0.0.1]"
        || host.StartsWith("[::ffff:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// IP アドレスがプライベートネットワークまたはループバックアドレスかどうかを判定する。
    /// </summary>
    /// <param name="address">チェック対象の IP アドレス。</param>
    /// <returns>プライベートまたはループバックの場合は <c>true</c>。</returns>
    /// <remarks>
    /// <para>
    /// <b>チェック対象:</b>
    /// <list type="bullet">
    ///   <item><description>ループバック: <c>127.0.0.0/8</c>, <c>::1</c></description></item>
    ///   <item><description>IPv6 ULA: <c>fc00::/7</c></description></item>
    ///   <item><description>IPv6 リンクローカル: <c>fe80::/10</c></description></item>
    ///   <item><description>IPv4 プライベート: <c>10.0.0.0/8</c>, <c>172.16.0.0/12</c>, <c>192.168.0.0/16</c></description></item>
    ///   <item><description>IPv4 リンクローカル: <c>169.254.0.0/16</c></description></item>
    ///   <item><description>ゼロアドレス: <c>0.0.0.0/8</c></description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>IPv6-mapped IPv4:</b> <c>::ffff:x.x.x.x</c> 形式のアドレスは
    /// IPv4 にアンラップしてからチェックする。
    /// </para>
    /// </remarks>
    private static bool IsPrivateOrLoopback(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        // IPv6 プライベート / リンクローカルを先にチェック
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            // fc00::/7 — ユニークローカルアドレス (ULA)
            if ((bytes[0] & 0xFE) == 0xFC)
                return true;
            // fe80::/10 — リンクローカルアドレス
            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
                return true;
        }

        // IPv6-mapped IPv4 をアンラップ
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        var ipv4Bytes = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork
            && ipv4Bytes switch
            {
                [10, ..] => true,                                      // 10.0.0.0/8
                [172, >= 16 and <= 31, ..] => true,                    // 172.16.0.0/12
                [192, 168, ..] => true,                                // 192.168.0.0/16
                [169, 254, ..] => true,                                // 169.254.0.0/16 (link-local)
                [127, ..] => true,                                     // 127.0.0.0/8
                [0, ..] => true,                                       // 0.0.0.0/8
                _ => false
            };
    }
}
