namespace PaymentCartService.Models.ValueObjects;

public readonly record struct Money(decimal Amount, string CurrencyCode = "JPY")
{
    public Money Add(Money other)
    {
        if (CurrencyCode != other.CurrencyCode)
            throw new InvalidOperationException("通貨単位が異なります");
        return this with { Amount = Amount + other.Amount };
    }

    public Money Subtract(Money other)
    {
        if (CurrencyCode != other.CurrencyCode)
            throw new InvalidOperationException("通貨単位が異なります");
        return this with { Amount = Amount - other.Amount };
    }
}
