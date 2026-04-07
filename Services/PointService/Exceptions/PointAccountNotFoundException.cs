namespace PointService.Exceptions;

/// <summary>ポイントアカウントが見つからない場合の例外 (PNT-4001)。</summary>
public class PointAccountNotFoundException : PointException
{
    public PointAccountNotFoundException(string userId)
        : base("PNT-4001", $"ポイントアカウントが見つかりません: UserId={userId}") { }
}
