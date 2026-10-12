using BenchmarkDotNet.Attributes;
using HtmlToPdfDotNet.Library;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Layout;
using HtmlToPdfDotNet.Library.Models.Writer;

namespace HtmlToPdfDotNet.Benchmarks;

/// <summary>
/// Measures the HTML-to-PDF pipeline per stage: the full conversion, style resolution plus layout,
/// and PDF writing alone, for each representative input under <c>Inputs/</c>.
/// </summary>
[MemoryDiagnoser]
public class ConversionBenchmarks
{
    private readonly ConversionOptions _options = new();
    private PdfGenerator _generator = null!;
    private PdfDocumentWriter _writer = null!;
    private MemoryStream _output = null!;
    private string _html = string.Empty;
    private LayoutResult _layout = null!;

    /// <summary>
    /// Gets the input file names (without extension) located in the <c>Inputs</c> folder.
    /// </summary>
    public static IEnumerable<string> Inputs => ["small", "table-report", "flex-layout"];

    /// <summary>
    /// Gets or sets the input currently being measured.
    /// </summary>
    [ParamsSource(nameof(Inputs))]
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Loads the input HTML and precomputes the layout used by <see cref="WritePdf"/>.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Inputs", Input + ".html");
        _html = File.ReadAllText(path);
        _generator = new PdfGenerator(_options);
        _writer = new PdfDocumentWriter(_options.Page, _options.CompressStreams);
        _output = new MemoryStream();

        // The writer only reads the layout, so one precomputed result is reused across iterations.
        // It skips the page-margin shift done by PdfGenerator.Convert, which moves coordinates
        // but does not change the amount of work the writer performs.
        _layout = _generator.RunLayout(_html);
    }

    /// <summary>
    /// Releases the reusable output stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => _output.Dispose();

    /// <summary>
    /// Full conversion: parse, style, layout, and PDF writing.
    /// </summary>
    /// <returns>The generated PDF bytes.</returns>
    [Benchmark]
    public byte[] Convert() => _generator.Convert(_html);

    /// <summary>
    /// HTML parsing, CSS style resolution, and layout only.
    /// </summary>
    /// <returns>The computed layout.</returns>
    [Benchmark]
    public LayoutResult RunLayout() => _generator.RunLayout(_html);

    /// <summary>
    /// PDF writing only, from a layout precomputed in <see cref="Setup"/>.
    /// </summary>
    /// <returns>The number of bytes written.</returns>
    [Benchmark]
    public long WritePdf()
    {
        _output.SetLength(0);
        _writer.Write(_layout, _output);
        return _output.Length;
    }
}
