namespace SalesManagementService.Services.Interfaces;

public interface IOutboxWriter
{
    Task WriteAsync<T>(string eventType, string aggregateId, T payload, CancellationToken ct = default);
}
