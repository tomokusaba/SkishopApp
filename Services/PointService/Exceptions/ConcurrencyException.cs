namespace PointService.Exceptions;

/// <summary>楽観的ロック競合の例外 (PNT-4005)。</summary>
public class ConcurrencyException : PointException
{
    public ConcurrencyException()
        : base("PNT-4005", "データが他のユーザーによって更新されました。再度お試しください。") { }

    public ConcurrencyException(string message, Exception innerException)
        : base("PNT-4005", message, innerException) { }
}
