using System.Text.Json;
using MailSendService.DTOs;
using MailSendService.Exceptions;
using MailSendService.Services.Interfaces;

namespace MailSendService.Services;

/// <summary>
/// メールイベントのペイロードからテンプレート名・送信先・変数を解決する。
/// 各イベント種別に対応するリゾルバーメソッドを提供する。
/// </summary>
public partial class MailEventResolver(
    IUserInfoResolver userInfoResolver,
    ILogger<MailEventResolver> logger) : IMailEventResolver
{
    /// <inheritdoc />
    public async Task<ResolvedEventData> ResolveEventDataAsync(
        string eventType, string payload, string correlationId, CancellationToken ct = default)
    {
        LogEventResolving(eventType);

        var (templateName, email, name, userId, variables) = eventType switch
        {
            "user.registered" => ResolveUserRegistered(payload),
            "password.reset.requested" => ResolvePasswordReset(payload),
            "user.verified" => ResolveUserVerified(payload),
            "order.created" => await ResolveOrderEvent(payload, "order-confirmation", correlationId, ct),
            "order.cancelled" => await ResolveOrderEvent(payload, "order-cancelled", correlationId, ct),
            "shipment.status.updated" => await ResolveShipmentEvent(payload, correlationId, ct),
            "user.email_changed" => ResolveEmailChanged(payload),
            _ => throw new EventDeserializationException($"Unsupported event type: {eventType}")
        };

        return new ResolvedEventData(templateName, email, name, userId, variables);
    }

    /// <summary>
    /// user.registered イベントのペイロードからメール認証テンプレート用のデータを抽出する。
    /// </summary>
    private static (string, string, string?, string?, Dictionary<string, object>) ResolveUserRegistered(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var email = root.GetProperty("email").GetString()
            ?? throw new EventDeserializationException("email is required in user.registered event");
        var userId = root.TryGetProperty("userId", out var uid) ? uid.GetString() : null;
        var token = root.TryGetProperty("verificationToken", out var vt) ? vt.GetString() : string.Empty;
        var variables = new Dictionary<string, object>
        {
            ["email"] = email,
            ["verificationToken"] = token ?? string.Empty
        };
        return ("email-verification", email, null, userId, variables);
    }

    /// <summary>
    /// password.reset.requested イベントのペイロードからパスワードリセットテンプレート用のデータを抽出する。
    /// </summary>
    private static (string, string, string?, string?, Dictionary<string, object>) ResolvePasswordReset(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var email = root.GetProperty("email").GetString()
            ?? throw new EventDeserializationException("email is required in password.reset.requested event");
        var userId = root.TryGetProperty("userId", out var uid) ? uid.GetString() : null;
        var token = root.TryGetProperty("resetToken", out var rt) ? rt.GetString() : string.Empty;
        var variables = new Dictionary<string, object>
        {
            ["email"] = email,
            ["resetToken"] = token ?? string.Empty
        };
        return ("password-reset", email, null, userId, variables);
    }

    /// <summary>
    /// user.verified イベントのペイロードからウェルカムメールテンプレート用のデータを抽出する。
    /// </summary>
    private static (string, string, string?, string?, Dictionary<string, object>) ResolveUserVerified(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var email = root.GetProperty("email").GetString()
            ?? throw new EventDeserializationException("email is required in user.verified event");
        var userId = root.TryGetProperty("userId", out var uid) ? uid.GetString() : null;
        var name = root.TryGetProperty("firstName", out var fn) ? fn.GetString() : null;
        var variables = new Dictionary<string, object>
        {
            ["email"] = email,
            ["firstName"] = name ?? string.Empty
        };
        return ("welcome", email, name, userId, variables);
    }

    /// <summary>
    /// 注文系イベント（order.created / order.cancelled）のペイロードからテンプレート用データを抽出する。
    /// UserManagementService からユーザー情報を解決する。
    /// </summary>
    private async Task<(string, string, string?, string?, Dictionary<string, object>)> ResolveOrderEvent(
        string payload, string templateName, string correlationId, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var customerId = root.GetProperty("customerId").GetString()
            ?? throw new EventDeserializationException("customerId is required in order event");
        var orderId = root.GetProperty("orderId").GetString()
            ?? throw new EventDeserializationException("orderId is required in order event");

        var userInfo = await userInfoResolver.ResolveAsync(customerId, correlationId, ct)
            ?? throw new UserInfoResolutionException($"ユーザー情報が見つかりません: {customerId}");

        var variables = new Dictionary<string, object>
        {
            ["orderId"] = orderId,
            ["email"] = userInfo.Email,
            ["firstName"] = userInfo.FirstName,
            ["lastName"] = userInfo.LastName
        };

        if (root.TryGetProperty("totalAmount", out var amount))
            variables["totalAmount"] = amount.GetDecimal().ToString("N0");

        return (templateName, userInfo.Email, $"{userInfo.FirstName} {userInfo.LastName}", customerId, variables);
    }

    /// <summary>
    /// 配送ステータス更新イベントのペイロードからテンプレート用データを抽出する。
    /// ステータスに応じて shipment-notification（出荷通知）または delivery-confirmation（配達完了）テンプレートを選択する。
    /// </summary>
    private async Task<(string, string, string?, string?, Dictionary<string, object>)> ResolveShipmentEvent(
        string payload, string correlationId, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var customerId = root.GetProperty("customerId").GetString()
            ?? throw new EventDeserializationException("customerId is required in shipment event");
        var orderId = root.GetProperty("orderId").GetString()
            ?? throw new EventDeserializationException("orderId is required in shipment event");
        var status = root.GetProperty("status").GetString()
            ?? throw new EventDeserializationException("status is required in shipment event");

        var templateName = status switch
        {
            "SHIPPED" => "shipment-notification",
            "DELIVERED" => "delivery-confirmation",
            _ => throw new EventDeserializationException($"Unsupported shipment status: {status}")
        };

        var userInfo = await userInfoResolver.ResolveAsync(customerId, correlationId, ct)
            ?? throw new UserInfoResolutionException($"ユーザー情報が見つかりません: {customerId}");

        var variables = new Dictionary<string, object>
        {
            ["orderId"] = orderId,
            ["email"] = userInfo.Email,
            ["firstName"] = userInfo.FirstName,
            ["status"] = status
        };

        if (root.TryGetProperty("trackingNumber", out var tn))
            variables["trackingNumber"] = tn.GetString() ?? string.Empty;

        return (templateName, userInfo.Email, $"{userInfo.FirstName} {userInfo.LastName}", customerId, variables);
    }

    /// <summary>
    /// user.email_changed イベントのペイロードからメールアドレス変更認証テンプレート用のデータを抽出する。
    /// </summary>
    private static (string, string, string?, string?, Dictionary<string, object>) ResolveEmailChanged(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var newEmail = root.GetProperty("newEmail").GetString()
            ?? throw new EventDeserializationException("newEmail is required in user.email_changed event");
        var userId = root.TryGetProperty("userId", out var uid) ? uid.GetString() : null;
        var token = root.TryGetProperty("verificationToken", out var vt) ? vt.GetString() : string.Empty;
        var variables = new Dictionary<string, object>
        {
            ["newEmail"] = newEmail,
            ["verificationToken"] = token ?? string.Empty
        };
        return ("email-change-verification", newEmail, null, userId, variables);
    }

    // --- LoggerMessage ソースジェネレーター（ホットパス最適化） ---

    [LoggerMessage(Level = LogLevel.Information, Message = "イベント解決: EventType={EventType}")]
    partial void LogEventResolving(string eventType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "未知のイベント種別: EventType={EventType}")]
    partial void LogUnknownEventType(string eventType);
}
