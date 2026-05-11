using System.Text.RegularExpressions;
using HtmlToPdfDotNet.Library.Commons;

namespace HtmlToPdfDotNet.Library.Models.Imaging;

/// <summary>
/// Loads an image from the given source.
/// Supports: local files, base64 data URIs.
/// </summary>
public static class ImageLoader
{

    /// <summary>
    /// Loads an image from the given source.
    /// Supports: local files, base64 data URIs.
    /// </summary>
    /// <param name="src">The image source.</param>
    /// <param name="basePath">The base path to resolve relative paths.</param>
    /// <returns><see cref="ImageData"/></returns>
    public static ImageData Load(string src, string? basePath = null)
    {
        if (string.IsNullOrEmpty(src))
        {
            throw new ArgumentException("Image source cannot be null or empty", nameof(src));
        }

        // Data URI: data:image/png;base64,...
        if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return LoadFromDataUri(src);
        }

        // Local file
        string path = ResolveFilePath(src, basePath);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Image file not found: {path}", path);
        }

        byte[] bytes = File.ReadAllBytes(path);
        return LoadFromBytes(bytes);
    }

    /// <summary>
    /// Loads an image from raw bytes.
    /// </summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <returns><see cref="ImageData"/></returns>
    public static ImageData LoadFromBytes(byte[] bytes)
    {
        ImageFormat format = DetectFormat(bytes);
        return format switch
        {
            ImageFormat.Jpeg => LoadJpeg(bytes),
            ImageFormat.Png => PngDecoder.Decode(bytes),
            _ => throw new InvalidOperationException("Unsupported image format")
        };
    }

    /// <summary>
    /// Detects the image format from the given bytes.
    /// Supports JPEG and PNG images.
    /// </summary>
    /// <param name="data">The raw image bytes.</param>
    /// <returns><see cref="ImageFormat"/></returns>
    private static ImageFormat DetectFormat(byte[] data)
    {
        if (data.Length < 3)
        {
            return ImageFormat.Unknown;
        }

        // JPEG
        if (data.AsSpan().StartsWith(Constants.JpegSignature))
        {
            return ImageFormat.Jpeg;
        }

        // PNG
        if (data.AsSpan().StartsWith(Constants.PngSignature))
        {
            return ImageFormat.Png;
        }

        return ImageFormat.Unknown;
    }

    #region JPEG
    /// <summary>
    /// Loads a JPEG image from raw bytes.
    /// </summary>
    /// <param name="jpegBytes">The raw JPEG image bytes.</param>
    /// <returns><see cref="ImageData"/></returns>
    private static ImageData LoadJpeg(byte[] jpegBytes)
    {
        (int width, int height) = ReadJpegDimensions(jpegBytes);
        return new ImageData
        {
            PixelWidth = width,
            PixelHeight = height,
            Format = ImageFormat.Jpeg,
            RawBytes = jpegBytes, // original bytes of jpeg image
            ColorSpace = Constants.ColorSpaceRgb, // commonly RGB (CMYK requires conversion)
            BitsPerComponent = 8 // 8 bits per channel for jpeg images
        };
    }

    /// <summary>
    /// Reads the dimensions of a JPEG image from the given bytes.
    /// </summary>
    /// <param name="data">The raw JPEG image bytes.</param>
    /// <returns><see cref="(int width, int height)"/></returns>
    private static (int width, int height) ReadJpegDimensions(byte[] data)
    {
        int i = 2; // skip marker FF D8
        while (i + 4 < data.Length)
        {
            if (data[i] != 0xFF) break;

            byte marker = data[i + 1];
            i += 2;

            // SOF markers: C0..CF (except C4, C8, CC)
            if (marker >= 0xC0 && marker <= 0xCF &&
                marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                if (i + 5 >= data.Length) break;
                int segmentSize = (data[i] << 8) | data[i + 1];
                if (i + segmentSize > data.Length) break;

                int height = (data[i + 3] << 8) | data[i + 4];
                int width = (data[i + 5] << 8) | data[i + 6];
                return (width, height);
            }

            // Read size of segment and skip
            if (marker == 0xD8 || marker == 0xD9) continue; // SOI/EOI no data.
            if (i + 2 > data.Length) break;
            int len = (data[i] << 8) | data[i + 1];
            i += len;
        }

        throw new InvalidOperationException("Not found marker SOF in jpeg image.");
    }
    #endregion

    /// <summary>
    /// Loads an image from a data URI.
    /// Format: data:image/{format};base64,{data}
    /// </summary>
    /// <param name="dataUri">The data URI.</param>
    /// <returns>Image data.</returns>
    private static ImageData LoadFromDataUri(string dataUri)
    {
        //data:image/png;base64,
        Match match = Regex.Match(dataUri, @"^data:image/[^;]+;base64,(.+)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            throw new ArgumentException("Invalid data URI format", nameof(dataUri));
        }

        string base64 = match.Groups[1].Value;
        byte[] bytes = Convert.FromBase64String(base64);
        return LoadFromBytes(bytes);
    }

    /// <summary>
    /// Resolves image file path.
    /// </summary>
    /// <param name="src">The image source.</param>
    /// <param name="basePath">The base path.</param>
    /// <returns>Path to the image file.</returns>
    private static string ResolveFilePath(string src, string? basePath)
    {
        // absolute path
        if (Path.IsPathRooted(src)) return src;

        // relative path
        if (string.IsNullOrEmpty(basePath))
        {
            basePath = Directory.GetCurrentDirectory();
        }

        return Path.GetFullPath(Path.Combine(basePath, src));
    }
}