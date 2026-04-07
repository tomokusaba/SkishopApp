namespace Frontend.Models;

public record StockChangeEvent(string ProductId, int CurrentStock, int PreviousStock, DateTime Timestamp);
public record OrderStatusEvent(string OrderId, string Status, string? Message, DateTime Timestamp);
