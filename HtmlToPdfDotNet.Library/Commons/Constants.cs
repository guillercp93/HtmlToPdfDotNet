namespace HtmlToPdfDotNet.Library.Commons;

/// <summary>
/// Contains global constants used throughout the library.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Default font size in points.
    /// </summary>
    public const float DefaultFontSize = 12f;

    /// <summary>
    /// Conversion factor from 96 DPI pixels to 72 DPI points (72/96 = 0.75).
    /// </summary>
    public const float PointsPerPx = 0.75f;

    /// <summary>
    /// 72 points per inch, PDF standard.
    /// </summary>
    public const float PointsPerInch = 72f;

    /// <summary>
    /// Conversion factor from inches to points (72 points per inch).
    /// </summary>
    public const float PointsPerCm = 28.3465f;

    /// <summary>
    /// Conversion factor from millimeters to points (2.83465 points per millimeter).
    /// </summary>
    public const float PointsPerMm = 2.83465f;

    /// <summary>PDF name for the RGB color space.</summary>
    public const string ColorSpaceRgb = "/DeviceRGB";

    /// <summary>PDF name for the Grayscale color space.</summary>
    public const string ColorSpaceGray = "/DeviceGray";

    /// <summary>PDF name for the CMYK color space.</summary>
    public const string ColorSpaceCmyk = "/DeviceCMYK";

    /// <summary>
    /// The 8-byte signature that identifies a PNG file.
    /// </summary>
    public static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// The signature that identifies a JPEG file (SOI marker).
    /// </summary>
    public static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
}
