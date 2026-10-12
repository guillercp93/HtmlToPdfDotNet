using BenchmarkDotNet.Attributes;
using HtmlAgilityPack;

namespace HtmlToPdfDotNet.Benchmarks;

/// <summary>
/// Isolates the cost of keying a per-node map by freshly parsed <see cref="HtmlNode"/> instances
/// (first identity hash of each node) from the cost of parsing the document itself.
/// Subtract <see cref="ParseOnly"/> from the other benchmarks to get the map cost.
/// </summary>
[MemoryDiagnoser]
public class StyleMapBenchmarks
{
    private static readonly object Value = new();
    private string _html = string.Empty;

    /// <summary>
    /// Gets or sets the input currently being measured.
    /// </summary>
    [Params("table-report", "flex-layout")]
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Loads the input HTML once.
    /// </summary>
    [GlobalSetup]
    public void Setup()
        => _html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Inputs", Input + ".html"));

    /// <summary>
    /// Parses the document and walks every node without storing anything (baseline).
    /// </summary>
    /// <returns>The number of element and text nodes visited.</returns>
    [Benchmark(Baseline = true)]
    public int ParseOnly()
    {
        int count = 0;
        Walk(Parse(), _ => count++);
        return count;
    }

    /// <summary>
    /// Parses the document and inserts every element and text node into a default-capacity dictionary,
    /// as <c>StyleResolver.Resolve</c> does.
    /// </summary>
    /// <returns>The number of entries stored.</returns>
    [Benchmark]
    public int ParseAndDictionary()
    {
        Dictionary<HtmlNode, object> map = new();
        Walk(Parse(), node => map[node] = Value);
        return map.Count;
    }

    /// <summary>
    /// Parses the document and only computes the identity hash of every node, isolating hashing from storage.
    /// </summary>
    /// <returns>A checksum of the hashes.</returns>
    [Benchmark]
    public int ParseAndHashOnly()
    {
        int sum = 0;
        Walk(Parse(), node => sum ^= System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(node));
        return sum;
    }

    /// <summary>
    /// Same as <see cref="ParseAndDictionary"/> but with the capacity pre-sized from a node count.
    /// </summary>
    /// <returns>The number of entries stored.</returns>
    [Benchmark]
    public int ParseAndPresizedDictionary()
    {
        HtmlNode root = Parse();
        int count = 0;
        Walk(root, _ => count++);
        Dictionary<HtmlNode, object> map = new(count);
        Walk(root, node => map[node] = Value);
        return map.Count;
    }

    /// <summary>
    /// Candidate alternative: an ordered store that keeps every node and its value in traversal order
    /// (no hashing at all), as an array-indexed style store would.
    /// </summary>
    /// <returns>The number of entries stored.</returns>
    [Benchmark]
    public int ParseAndOrderedStore()
    {
        List<KeyValuePair<HtmlNode, object>> list = new();
        Walk(Parse(), node => list.Add(new(node, Value)));
        return list.Count;
    }

    private HtmlNode Parse()
    {
        HtmlDocument doc = new();
        doc.LoadHtml(_html);
        return doc.DocumentNode;
    }

    private static void Walk(HtmlNode node, Action<HtmlNode> visit)
    {
        if (node.NodeType is HtmlNodeType.Element or HtmlNodeType.Text)
            visit(node);
        foreach (HtmlNode child in node.ChildNodes)
            Walk(child, visit);
    }
}
