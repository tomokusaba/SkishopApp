using System.Security.Cryptography;

namespace SalesManagementService.Services;

public class OrderNumberGenerator(TimeProvider timeProvider)
{
    public string GenerateOrderNumber()
    {
        var now = timeProvider.GetUtcNow();
        var seq = RandomNumberGenerator.GetInt32(0, 100000);
        return $"ORD-{now:yyyyMMdd}-{seq:D5}";
    }

    public string GenerateReturnNumber()
    {
        var now = timeProvider.GetUtcNow();
        var seq = RandomNumberGenerator.GetInt32(0, 100000);
        return $"RTN-{now:yyyyMMdd}-{seq:D5}";
    }
}
