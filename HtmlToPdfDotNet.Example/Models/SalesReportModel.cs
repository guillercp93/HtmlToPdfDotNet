namespace HtmlToPdfDotNet.Example.Models;

public record SalesItem(string Product, string Category, int Units, decimal UnitPrice)
{
    public decimal Total => Units * UnitPrice;
}

public class SalesReportModel
{
    private static readonly Lazy<string> _logoDataUri = new(() =>
    {
        // Resolve logo relative to the assembly location so it works from any working directory.
        string assemblyDir = AppContext.BaseDirectory;

        // Walk up until we find wwwroot (handles bin/Debug/net10.0 nesting).
        string? dir = assemblyDir;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "wwwroot", "images", "logo.png");
            if (File.Exists(candidate))
            {
                byte[] bytes = File.ReadAllBytes(candidate);
                return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
            }
            dir = Path.GetDirectoryName(dir);
        }

        return string.Empty; // logo not found — img tag will be hidden
    });

    /// <summary>Base64-encoded data URI for the company logo PNG.</summary>
    public string LogoDataUri => _logoDataUri.Value;

    public string Title { get; init; } = "Quarterly Sales Report";
    public string Subtitle { get; init; } = "Q1 2026 — Executive Summary";
    public string GeneratedAt { get; init; } = DateTime.Now.ToString("MMMM dd, yyyy HH:mm");
    public string CompanyName { get; init; } = "Acme Corporation";

    public IReadOnlyList<SalesItem> Items { get; init; } =
    [
        new("Laptop Pro 15\"",   "Electronics",  142,  1_299.99m),
        new("Wireless Keyboard", "Accessories",  378,    89.99m),
        new("4K Monitor 27\"",   "Electronics",   95,    549.00m),
        new("USB-C Hub 7-in-1",  "Accessories",  521,    45.50m),
        new("Noise-Cancel Headphones", "Audio",  210,    199.00m),
        new("Mechanical Mouse",  "Accessories",  430,    69.99m),
        new("Webcam 4K",         "Electronics",  183,    129.00m),
        new("Desk Lamp LED",     "Office",       260,    39.99m),
        new("Ergonomic Chair",   "Furniture",     47,    699.00m),
        new("Standing Desk",     "Furniture",     31,  1_199.00m),
    ];

    public decimal GrandTotal => Items.Sum(i => i.Total);
    public int TotalUnits => Items.Sum(i => i.Units);
}
