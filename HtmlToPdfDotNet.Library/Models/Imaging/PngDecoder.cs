using System.Buffers.Binary;
using HtmlToPdfDotNet.Library.Commons;

namespace HtmlToPdfDotNet.Library.Models.Imaging;

/// <summary>
/// Decoder pure PNG image in C#
/// It supports:
/// - Color type 0 -> Grayscale
/// - Color type 2 -> RGB
/// - Color type 3 -> Indexed (PLTE) expanded to RGB
/// - Color type 4 -> Grayscale + alpha
/// - Color type 6 -> RGBA
/// - Bit depths 8 and 16 (16-bit is reduced to 8-bit)
/// It doesn't support
/// - Interlaced PNG (Adam7)
/// </summary>
public static class PngDecoder
{
    public static ImageData Decode(byte[] pngBytes)
    {
        ValidateSignature(pngBytes);

        ChunkReader reader = new ChunkReader(pngBytes, 8);
        // IHDR should be the first chunk
        Chunk ihdr = reader.ReadChunk();
        if (ihdr.Type != "IHDR")
        {
            throw new InvalidDataException("PNG: First chunk isn't IHDR.");
        }

        int width = BinaryPrimitives.ReadInt32BigEndian(ihdr.Data.AsSpan(0, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(ihdr.Data.AsSpan(4, 4));

        int bitDepth = ihdr.Data[8];
        int colorType = ihdr.Data[9];
        int interlace = ihdr.Data[12];

        if (interlace != 0)
        {
            throw new InvalidDataException("PNG: Interlacing (Adam7) is not supported.");
        }

        // Read chunks IDAT and PLTE
        List<byte[]> idatData = new();
        byte[]? plte = null;

        while (true)
        {
            var chunk = reader.ReadChunk();
            if (chunk.Type == "IEND") break;
            if (chunk.Type == "IDAT") idatData.Add(chunk.Data);
            if (chunk.Type == "PLTE") plte = chunk.Data;
        }

        // Descompress IDAT (zlib/FlateDecode)
        byte[] compressed = Helpers.ConcatArrays(idatData);
        byte[] rawScanlines = Helpers.ZlibDecompress(compressed);

        // Parameters by colorType
        GetColorTypeParams(colorType, out int samplesPerPixel, out string colorSpace, out bool hasAlpha);

        int bytePerSamples = bitDepth == 16 ? 2 : 1;
        int bpp = bytePerSamples * samplesPerPixel; // bytes per pixel
        int stride = width * bpp + 1; // +1 for filter type byte

        if (rawScanlines.Length < (height * stride))
        {
            throw new InvalidDataException(
                $"PNG: IDAT data isn't enough. Expected: {height * stride} bytes, got: {rawScanlines.Length} bytes."
            );
        }

        // Undo filtering of scanlines
        UnfilterScanlines(rawScanlines, height, bpp, stride);

        // Extract pixels normalized to 8-bit
        (byte[] rgbBytes, byte[]? alphaBytes) = ExtractPixels(rawScanlines,
                                                              width,
                                                              height,
                                                              colorType,
                                                              bitDepth,
                                                              samplesPerPixel,
                                                              hasAlpha,
                                                              plte);
        return new ImageData
        {
            PixelHeight = height,
            PixelWidth = width,
            Format = ImageFormat.Png,
            RawBytes = rgbBytes,
            AlphaBytes = alphaBytes,
            ColorSpace = colorSpace,
            BitsPerComponent = 8
        };
    }

    /// <summary>
    /// Extract decompressed pixels separating RGB and alpha channels.
    /// All of values are normalized to 8-bit.
    /// </summary>
    /// <param name="data">Raw scanlines after filter undone.</param>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    /// <param name="colorType">Color type.</param>
    /// <param name="bitDepth">Bit depth.</param>
    /// <param name="samplesPerPixel">Samples per pixel.</param>
    /// <param name="hasAlpha">Has alpha.</param>
    /// <param name="plte">Palette.</param>
    /// <returns>Tuple of RGB and alpha bytes.</returns>
    private static (byte[] rgb, byte[]? alpha) ExtractPixels(byte[] data,
                                                             int width,
                                                             int height,
                                                             int colorType,
                                                             int bitDepth,
                                                             int samplesPerPixel,
                                                             bool hasAlpha,
                                                             byte[]? plte)
    {
        int pixelCount = width * height;
        int rgbComponents = colorType is 0 or 4 ? 1 : 3; // Gray or RGB
        int bytesPerSample = bitDepth == 16 ? 2 : 1; // 16-bit is reduced to 8-bit
        int stride = width * samplesPerPixel * bytesPerSample + 1; // +1 for filter type byte

        byte[] rgbOut = new byte[pixelCount * rgbComponents];
        byte[]? alphaOut = hasAlpha ? new byte[pixelCount] : null;

        int outIdx = 0, alphaIdx = 0;
        int rowStart, src;

        for (int y = 0; y < height; y++)
        {
            rowStart = y * stride + 1; // position of the first pixel of the row (skip filter)

            for (int x = 0; x < width; x++)
            {
                src = rowStart + x * samplesPerPixel * bytesPerSample; // index in raw scanlines

                switch (colorType)
                {
                    case 0: // GrayScale
                        rgbOut[outIdx++] = data[src];
                        break;
                    case 2: // RGB
                        rgbOut[outIdx++] = data[src];
                        rgbOut[outIdx++] = data[src + bytesPerSample];
                        rgbOut[outIdx++] = data[src + bytesPerSample * 2];
                        break;
                    case 3: // Indexed -> expand to RGB using PLTE
                        int paletteIdx = data[src];
                        if (plte != null && (paletteIdx * 3 + 2) < plte.Length)
                        {
                            rgbOut[outIdx++] = plte[paletteIdx * 3];
                            rgbOut[outIdx++] = plte[paletteIdx * 3 + 1];
                            rgbOut[outIdx++] = plte[paletteIdx * 3 + 2];
                        }
                        else
                        {
                            rgbOut[outIdx++] = 0;
                            rgbOut[outIdx++] = 0;
                            rgbOut[outIdx++] = 0;
                        }
                        break;
                    case 4: // Grayscale + alpha
                        rgbOut[outIdx++] = data[src];
                        if (alphaOut != null) alphaOut[alphaIdx++] = data[src + bytesPerSample];
                        break;
                    case 6: // RGBA
                        rgbOut[outIdx++] = data[src];
                        rgbOut[outIdx++] = data[src + bytesPerSample];
                        rgbOut[outIdx++] = data[src + bytesPerSample * 2];
                        if (alphaOut != null) alphaOut[alphaIdx++] = data[src + bytesPerSample * 3];
                        break;
                    default:
                        throw new InvalidDataException($"PNG: Unsupported color type {colorType}.");
                }

            }

        }

        return (rgbOut, alphaOut);
    }

    /// <summary>
    /// Undo the PNG filter in-place. Each scanline begins with 1 byte of filter type.
    /// Filter type is stored as the first byte of each scanline.
    /// Type of filters:
    /// 0: None     -> No changes
    /// 1: Sub      -> It's diferent with left pixel
    /// 2: Up       -> It's diferent with upper pixel
    /// 3: Average  -> It's diferent with average of left and upper pixel
    /// 4: Paeth    -> It's diferent with the predictor Paeth algorithm
    /// </summary>
    /// <param name="data">Raw scanlines.</param>
    /// <param name="height">Image height.</param>
    /// <param name="bytesPerPixel">Bytes per pixel.</param>
    /// <param name="stride">It's the width of the scanline including the filter type byte.</param>
    private static void UnfilterScanlines(byte[] data, int height, int bytesPerPixel, int stride)
    {
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * stride;
            byte filterType = data[rowStart];
            int pixStart = rowStart + 1; // data in the actual row
            int prevStart = rowStart - stride + 1; // data in the previous row

            for (int x = 0; x < stride - 1; x++)
            {
                int i = pixStart + x;
                byte left = x >= bytesPerPixel ? data[i - bytesPerPixel] : (byte)0;
                byte up = y > 0 ? data[prevStart + x] : (byte)0;
                byte upLeft = y > 0 && x >= bytesPerPixel ? data[prevStart + x - bytesPerPixel] : (byte)0;

                data[i] = filterType switch
                {
                    0 => data[i], // None
                    1 => (byte)(data[i] + left), // Sub
                    2 => (byte)(data[i] + up), // Up
                    3 => (byte)(data[i] + (left + up) / 2), // Average
                    4 => (byte)(data[i] + PaethPredictor(left, up, upLeft)), // Paeth
                    _ => data[i]
                };
            }
        }
    }

    /// <summary>
    /// Paeth predictor algorithm.
    /// </summary>
    /// <param name="left">Left pixel.</param>
    /// <param name="up">Upper pixel.</param>
    /// <param name="upLeft">Upper-left pixel.</param>
    /// <returns>Predicted pixel.</returns>
    private static byte PaethPredictor(byte left, byte up, byte upLeft)
    {
        int p = left + up - upLeft;
        int pLeft = Math.Abs(p - left);
        int pUp = Math.Abs(p - up);
        int pUpLeft = Math.Abs(p - upLeft);

        if (pLeft <= pUp && pLeft <= pUpLeft) return left;
        if (pUp <= pUpLeft) return up;
        return upLeft;
    }

    /// <summary>
    /// Get the number of samples per pixel, the color space and if the image has alpha channel.
    /// </summary>
    /// <param name="colorType">Color type.</param>
    /// <param name="samplesPerPixel">Number of samples per pixel.</param>
    /// <param name="colorSpace">Color space.</param>
    /// <param name="hasAlpha">Has alpha channel.</param>
    private static void GetColorTypeParams(int colorType, out int samplesPerPixel, out string colorSpace, out bool hasAlpha)
    {
        (samplesPerPixel, colorSpace, hasAlpha) = colorType switch
        {
            0 => (1, Constants.ColorSpaceGray, false), // Grayscale
            2 => (3, Constants.ColorSpaceRgb, false), // RGB
            3 => (1, Constants.ColorSpaceRgb, false), // Indexed -> expanded to RGB (use PLTE)
            4 => (2, Constants.ColorSpaceGray, true), // Grayscale + alpha
            6 => (4, Constants.ColorSpaceRgb, true), // RGBA
            _ => throw new NotSupportedException($"PNG color type {colorType} is not supported.")
        };
    }

    /// <summary>
    /// Validates if the given data is a valid PNG file by checking its signature.
    /// </summary>
    /// <param name="data">The data to validate.</param>
    /// <exception cref="InvalidDataException">The data is not a valid PNG file.</exception>
    private static void ValidateSignature(byte[] data)
    {
        if (data.Length < 8)
        {
            throw new InvalidDataException("PNG: too short, no signature.");
        }

        if (!data.AsSpan().StartsWith(Constants.PngSignature))
        {
            throw new InvalidDataException("PNG: Bad signature.");
        }
    }
}