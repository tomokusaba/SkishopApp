using Serilog.Context;

namespace PaymentCartService.Infrastructure;

public static class CorrelationIdScope
{
    public static IDisposable Push(string? correlationId = null)
    {
        correlationId ??= Guid.NewGuid().ToString();
        return LogContext.PushProperty("CorrelationId", correlationId);
    }
}
