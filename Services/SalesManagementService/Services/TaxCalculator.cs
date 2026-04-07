namespace SalesManagementService.Services;

public static class TaxCalculator
{
    private const decimal StandardTaxRate = 0.10m;

    public static decimal CalculateStandardTax(decimal subtotal)
        => Math.Floor(subtotal * StandardTaxRate);
}
