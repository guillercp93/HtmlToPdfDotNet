using HtmlToPdfDotNet.Library.Models.Layout;

namespace HtmlToPdfDotNet.Library.Commons;

public class ConversionOptions
{
    /// <summary>
    /// Page settings (size and margins).
    /// </summary>
    public PageLayout Page { get; set; } = PageLayout.A4;

    /// <summary>
    /// If true, content streams are compressed using FlateDecode.
    /// </summary>
    public bool CompressStreams { get; set; } = true;
}
