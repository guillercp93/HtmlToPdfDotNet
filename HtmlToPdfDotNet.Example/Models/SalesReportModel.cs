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
    public string Subtitle { get; init; } = "Q1 2026 - Executive Summary";
    public string GeneratedAt { get; init; } = DateTime.Now.ToString("MMMM dd, yyyy HH:mm");
    public string CompanyName { get; init; } = "Acme Corporation";

    public IReadOnlyList<SalesItem> Items { get; init; } =
    [
        new("Laptop Pro 15\"",   "Electronics",  120,  1_499.00m),
        new("Wireless Headphones", "Audio",       250,    199.99m),
        new("Ergonomic Office Chair", "Furniture",  85,    349.50m),
        new("4K UltraWide Monitor", "Electronics",  60,    699.00m),
        new("Mechanical Keyboard", "Accessories", 410,    129.00m),
        new("Cable", "Electronics", 1000, 1.00m),
        new("Mousepad", "Accessories", 200, 20.00m),
        new("Wireless Mouse", "Accessories", 350, 25.00m),
        new("Desk Lamp", "Furniture", 120, 45.00m),
        new("Notebook", "Stationery", 800, 5.00m),
        new("Desk high adjuste", "Furniture", 10, 200m),
        new("bookshelf", "Furniture", 10, 200m),
        new("External Hard Drive", "Electronics", 150, 129.00m),
        new("USB-C Hub", "Accessories", 300, 49.99m),
        new("Smartwatch Series 5", "Electronics", 180, 299.00m),
        new("Noise-Cancelling Earbuds", "Audio", 150, 149.99m),
        new("Standing Desk", "Furniture", 45, 499.00m),
        new("Bluetooth Speaker", "Audio", 220, 79.99m),
        new("Webcam 1080p", "Accessories", 180, 89.99m),
        new("Wireless Charger", "Accessories", 400, 29.99m),
        new("LED Light Strip", "Furniture", 250, 19.99m),
        new("Leather Journal", "Stationery", 150, 25.00m),
        new("Gel Pen Pack", "Stationery", 600, 12.50m),
        new("Graphic Tablet", "Electronics", 70, 199.00m),
        new("Microphone Stand", "Audio", 90, 35.00m),
        new("Desk Mat", "Accessories", 300, 29.99m),
        new("Monitor Mount", "Furniture", 110, 75.00m),
        new("Paper Shredder", "Stationery", 50, 89.99m),
        new("HDMI Cable 10ft", "Accessories", 500, 9.99m),
        new("Smart Plug", "Electronics", 350, 15.00m),
        new("Filing Cabinet", "Furniture", 35, 150.00m),
        new("Dry Erase Board", "Stationery", 80, 40.00m),
        new("Stapler", "Stationery", 200, 8.50m),
        new("Desk Organizer", "Furniture", 150, 22.00m),
        new("Phone Stand", "Accessories", 450, 12.99m),
        new("Power Strip", "Accessories", 280, 18.50m),
        new("Label Maker", "Stationery", 95, 34.99m),
        new("Ring Light", "Accessories", 130, 45.00m),
        new("Tablet Holder", "Accessories", 160, 24.99m),
        new("Foot Rest", "Furniture", 120, 39.99m),
        new("Air Purifier", "Electronics", 75, 120.00m),
        new("Portable SSD 1TB", "Electronics", 110, 99.99m),
        new("Studio Headphones", "Audio", 80, 149.00m),
        new("Condenser Microphone", "Audio", 65, 119.00m),
        new("Sticky Notes Pack", "Stationery", 1000, 4.50m),
        new("Highlighters Pack", "Stationery", 400, 6.00m),
        new("Desk Clock", "Furniture", 140, 15.00m),
        new("Cable Clips Pack", "Accessories", 650, 5.99m),
        new("USB Flash Drive 64GB", "Electronics", 500, 10.99m),
        new("Ergonomic Keyboard", "Accessories", 90, 89.99m)
    ];

    public decimal GrandTotal => Items.Sum(i => i.Total);
    public int TotalUnits => Items.Sum(i => i.Units);
}
