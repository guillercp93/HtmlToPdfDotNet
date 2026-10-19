using BenchmarkDotNet.Attributes;
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Fonts;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Styles;

namespace HtmlToPdfDotNet.Benchmarks;

/// <summary>
/// Measures the per-run font resolution done by <see cref="Helpers.MakeRun"/>
/// (<see cref="FontRegistry.TryResolve"/> plus the <see cref="StandardFontMetrics.Resolve"/> fallback),
/// repeated as many times as the table-report input emits text primitives.
/// </summary>
[MemoryDiagnoser]
public class FontLookupBenchmarks
{
    private const string DejaVuPath = "/usr/share/fonts/TTF/DejaVuSans.ttf";
    private const string DejaVuDebianPath = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";

    // Font families as they reach MakeRun: StyleResolver.NormalizeFontFamily keeps only the first
    // family, unquoted, and maps generic and common names to the standard PDF names.
    private static readonly string[] Families = ["Helvetica", "Times-Roman", "Courier", "Helvetica", "Georgia"];

    private readonly FontRegistry _emptyRegistry = new ConversionOptions().Fonts;
    private readonly FontRegistry _embeddedRegistry = new();
    private ComputedStyle[] _styles = [];
    private string _embeddedFamily = "Helvetica";

    /// <summary>
    /// Gets the number of lookups per invocation: the text primitives emitted by table-report's layout.
    /// </summary>
    public int Lookups { get; private set; }

    /// <summary>
    /// Counts the table-report text primitives and prepares the registries and styles.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        string html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Inputs", "table-report.html"));
        LayoutResult layout = new PdfGenerator(new ConversionOptions()).RunLayout(html);
        Lookups = layout.Primitives.OfType<TextPrimitive>().Count();
        Console.WriteLine($"// FontLookup: table-report text primitives = {Lookups}");

        _styles = new ComputedStyle[Families.Length];
        for (int i = 0; i < Families.Length; i++)
            _styles[i] = new ComputedStyle { FontFamily = Families[i], FontWeight = i % 2 == 0 ? FontWeight.Normal : FontWeight.Bold };

        string path = File.Exists(DejaVuPath) ? DejaVuPath : DejaVuDebianPath;
        if (File.Exists(path))
        {
            _embeddedRegistry.RegisterFont(path, "DejaVu Sans");
            _embeddedFamily = "DejaVu Sans";
        }
    }

    /// <summary>
    /// Resolution against the default (empty) registry: every lookup misses and falls back to the standard fonts.
    /// </summary>
    /// <returns>A checksum of the resolved font names.</returns>
    [Benchmark(Baseline = true)]
    public int MakeRunDefaultRegistry() => Run(_emptyRegistry);

    /// <summary>
    /// Registry lookup alone against the default (empty) registry.
    /// </summary>
    /// <returns>The number of hits.</returns>
    [Benchmark]
    public int TryResolveDefaultRegistry()
    {
        int hits = 0;
        for (int i = 0; i < Lookups; i++)
        {
            ComputedStyle style = _styles[i % _styles.Length];
            if (_emptyRegistry.TryResolve(style.FontFamily, style.FontWeight == FontWeight.Bold, false, out _)) hits++;
        }
        return hits;
    }

    /// <summary>
    /// Standard-font fallback alone (what MakeRun calls when the registry misses).
    /// </summary>
    /// <returns>A checksum of the resolved font names.</returns>
    [Benchmark]
    public int StandardResolveOnly()
    {
        int sum = 0;
        for (int i = 0; i < Lookups; i++)
        {
            ComputedStyle style = _styles[i % _styles.Length];
            sum += StandardFontMetrics.Resolve(style.FontFamily, style.FontWeight == FontWeight.Bold, false).Length;
        }
        return sum;
    }

    /// <summary>
    /// Registry hit on a registered embedded font (DejaVu Sans when installed).
    /// </summary>
    /// <returns>The number of hits.</returns>
    [Benchmark]
    public int TryResolveEmbeddedHit()
    {
        int hits = 0;
        for (int i = 0; i < Lookups; i++)
        {
            if (_embeddedRegistry.TryResolve(_embeddedFamily, (i & 1) == 1, false, out _)) hits++;
        }
        return hits;
    }

    private int Run(FontRegistry registry)
    {
        int sum = 0;
        for (int i = 0; i < Lookups; i++)
            sum += Helpers.MakeRun("cell", _styles[i % _styles.Length], registry).FontName.Length;
        return sum;
    }
}
