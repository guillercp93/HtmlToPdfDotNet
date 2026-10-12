using BenchmarkDotNet.Running;

namespace HtmlToPdfDotNet.Benchmarks;

/// <summary>
/// Entry point for the HTML-to-PDF benchmark suite.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the benchmarks selected by the command-line arguments (for example <c>--filter '*'</c>).
    /// </summary>
    /// <param name="args">BenchmarkDotNet command-line arguments.</param>
    public static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
