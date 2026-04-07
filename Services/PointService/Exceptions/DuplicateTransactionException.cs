namespace PointService.Exceptions;

/// <summary>重複トランザクションの例外 (PNT-4004)。</summary>
public class DuplicateTransactionException : PointException
{
    public DuplicateTransactionException(string orderId, string transactionType)
        : base("PNT-4004", $"重複するトランザクションです: OrderId={orderId}, Type={transactionType}") { }
}
