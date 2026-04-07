namespace PointService.Exceptions;

/// <summary>ポイント有効期限切れの例外 (PNT-4003)。</summary>
public class PointExpiredException : PointException
{
    public PointExpiredException(string expiryId)
        : base("PNT-4003", $"ポイントの有効期限が切れています: ExpiryId={expiryId}") { }
}
