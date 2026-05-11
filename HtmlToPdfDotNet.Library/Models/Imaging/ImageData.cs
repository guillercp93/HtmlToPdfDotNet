using HtmlToPdfDotNet.Library.Commons;

namespace HtmlToPdfDotNet.Library.Models.Imaging;

public enum ImageFormat
{
    Unknown,
    Jpeg,
    Png
}

/// <summary>
/// Image data (resolved dimensions, raster data, etc.).
/// </summary>
public sealed class ImageData
{
    public int PixelWidth { get; init; }
    public int PixelHeight { get; init; }
    public ImageFormat Format { get; init; }
    public byte[] RawBytes { get; init; } = [];
    public string ColorSpace { get; init; } = Constants.ColorSpaceRgb;
    public int BitsPerComponent { get; init; } = 8;
    public bool HasAlpha { get; init; }
    public byte[]? AlphaBytes { get; init; }
}