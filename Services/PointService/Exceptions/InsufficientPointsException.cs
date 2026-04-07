namespace PointService.Exceptions;

/// <summary>ポイント残高不足の例外 (PNT-4002)。</summary>
public class InsufficientPointsException : PointException
{
    public InsufficientPointsException(int requested, int available)
        : base("PNT-4002", $"ポイント残高が不足しています: 要求={requested}, 利用可能={available}") { }
}
