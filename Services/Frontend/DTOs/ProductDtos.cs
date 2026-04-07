namespace Frontend.DTOs;

public record ProductDto(
    string Id,
    string Name,
    string Description,
    decimal Price,
    decimal? OriginalPrice,
    string CategoryId,
    string CategoryName,
    List<string> ImageUrls,
    int StockQuantity,
    List<string> Sizes,
    List<string> Colors,
    double AverageRating,
    int ReviewCount,
    DateTime CreatedAt);

public record SizeGuideDto(string CategoryId, string CategoryName, List<SizeGuideRow> Rows);
public record SizeGuideRow(string Size, string Chest, string Waist, string Hips, string Height);
public record ReviewDto(string Id, string UserName, int Rating, string Comment, DateTime CreatedAt);
public record CreateReviewRequest(int Rating, string Comment);
