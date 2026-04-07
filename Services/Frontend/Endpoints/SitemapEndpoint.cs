using System.Xml.Linq;

namespace Frontend.Endpoints;

/// <summary>
/// サイトマップ XML を生成するエンドポイント。
/// </summary>
public static class SitemapEndpoint
{
    private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public static void MapSitemapEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap.xml", GenerateSitemap)
            .AllowAnonymous()
            .ExcludeFromDescription();
    }

    private static IResult GenerateSitemap(HttpContext httpContext)
    {
        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var lastModified = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var urls = new List<(string Loc, string Priority, string ChangeFreq)>
        {
            ("/", "1.0", "daily"),
            ("/products", "0.9", "daily"),
            ("/auth/login", "0.5", "monthly"),
            ("/auth/register", "0.5", "monthly"),
            ("/cart", "0.4", "weekly"),
            ("/coupons", "0.6", "weekly"),
            ("/points", "0.5", "weekly"),
            ("/wishlists", "0.5", "weekly"),
            ("/orders", "0.4", "weekly"),
            ("/legal/tokushoho", "0.3", "yearly"),
            ("/legal/privacy", "0.3", "yearly"),
            ("/legal/terms", "0.3", "yearly"),
        };

        var urlElements = urls.Select(u => new XElement(SitemapNs + "url",
            new XElement(SitemapNs + "loc", $"{baseUrl}{u.Loc}"),
            new XElement(SitemapNs + "lastmod", lastModified),
            new XElement(SitemapNs + "changefreq", u.ChangeFreq),
            new XElement(SitemapNs + "priority", u.Priority)));

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(SitemapNs + "urlset", urlElements));

        return Results.Content(document.Declaration + document.ToString(), "application/xml");
    }
}
