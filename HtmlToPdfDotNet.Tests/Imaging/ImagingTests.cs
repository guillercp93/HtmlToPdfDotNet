using System;
using System.IO;
using System.Text;
using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Imaging;
using HtmlToPdfDotNet.Library.Models.Writer;
using Xunit;

namespace HtmlToPdfDotNet.Tests.Imaging;

public class ImagingTests
{
    [Fact]
    public void PngSignature_IsCorrect()
    {
        byte[] expected = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.Equal(expected, Constants.PngSignature);
    }

    [Fact]
    public void JpegSignature_IsCorrect()
    {
        byte[] expected = { 0xFF, 0xD8, 0xFF };
        Assert.Equal(expected, Constants.JpegSignature);
    }

    [Fact]
    public void DetectFormat_Png_ReturnsPng()
    {
        byte[] data = new byte[10];
        Constants.PngSignature.CopyTo(data, 0);
        
        var format = InvokeDetectFormat(data);
        Assert.Equal(ImageFormat.Png, format);
    }

    [Fact]
    public void DetectFormat_Jpeg_ReturnsJpeg()
    {
        byte[] data = new byte[10];
        Constants.JpegSignature.CopyTo(data, 0);
        
        var format = InvokeDetectFormat(data);
        Assert.Equal(ImageFormat.Jpeg, format);
    }

    [Fact]
    public void DetectFormat_Unknown_ReturnsUnknown()
    {
        byte[] data = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 };
        
        var format = InvokeDetectFormat(data);
        Assert.Equal(ImageFormat.Unknown, format);
    }

    [Fact]
    public void LoadFromDataUri_ValidPng_LoadsCorrectly()
    {
        // 1x1 red PNG
        string dataUri = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==";
        
        var imageData = ImageLoader.Load(dataUri);
        
        Assert.Equal(ImageFormat.Png, imageData.Format);
        Assert.Equal(1, imageData.PixelWidth);
        Assert.Equal(1, imageData.PixelHeight);
    }

    [Fact]
    public void PngDecoder_Decode_ValidPng_Succeeds()
    {
        // 1x1 white PNG
        byte[] pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");
        
        var imageData = PngDecoder.Decode(pngBytes);
        
        Assert.Equal(1, imageData.PixelWidth);
        Assert.Equal(1, imageData.PixelHeight);
        Assert.Equal(ImageFormat.Png, imageData.Format);
    }

    [Fact]
    public void PngDecoder_Decode_InvalidSignature_ThrowsException()
    {
        byte[] badData = { 0, 0, 0, 0, 0, 0, 0, 0 };
        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(badData));
    }

    private ImageFormat InvokeDetectFormat(byte[] data)
    {
        var method = typeof(ImageLoader).GetMethod("DetectFormat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (method == null) throw new Exception("DetectFormat method not found");
        return (ImageFormat)method.Invoke(null, new object[] { data })!;
    }

    [Fact]
    public void ImageLoader_Load_NullOrEmptyOrWhitespace_ThrowsExceptions()
    {
        Assert.Throws<ArgumentNullException>(() => ImageLoader.Load(null!));
        Assert.Throws<ArgumentException>(() => ImageLoader.Load(""));
        Assert.Throws<ArgumentException>(() => ImageLoader.Load("   "));
    }

    [Fact]
    public void ImageLoader_Load_OutsideBaseDirectory_ThrowsUnauthorizedAccessException()
    {
        Assert.Throws<UnauthorizedAccessException>(() => ImageLoader.Load("../outside.png", basePath: "/home/guiller/Projects/HtmlToPdfDotNet/HtmlToPdfDotNet.Tests"));
    }

    [Fact]
    public void ImageLoader_LoadFromBytes_ValidJpeg_LoadsCorrectly()
    {
        // Minimal JPEG byte array (SOI + SOF0 + EOI)
        byte[] jpegBytes = { 
            0xFF, 0xD8,                  // SOI
            0xFF, 0xC0,                  // SOF0
            0x00, 0x0B,                  // length of segment (11)
            0x08,                        // precision (8)
            0x00, 0x0A,                  // height (10)
            0x00, 0x14,                  // width (20)
            0x01,                        // components (1)
            0x01, 0x11, 0x00,            // component spec
            0xFF, 0xD9                   // EOI
        };

        var imageData = ImageLoader.LoadFromBytes(jpegBytes);
        Assert.Equal(ImageFormat.Jpeg, imageData.Format);
        Assert.Equal(20, imageData.PixelWidth);
        Assert.Equal(10, imageData.PixelHeight);
    }

    [Fact]
    public void ImageLoader_LoadFromBytes_InvalidJpegNoSof_ThrowsInvalidOperationException()
    {
        byte[] badJpeg = { 0xFF, 0xD8, 0xFF, 0xD9 };
        Assert.Throws<InvalidOperationException>(() => ImageLoader.LoadFromBytes(badJpeg));
    }

    [Fact]
    public void ImageResourceBuilder_RegisterImage_DuplicateAlias_ReturnsExistingNumber()
    {
        var counter = new ObjectCounter();
        var xref = new XRefTable();
        var builder = new ImageResourceBuilder(counter, xref, compress: false);

        var img = new ImageData
        {
            PixelWidth = 10,
            PixelHeight = 10,
            Format = ImageFormat.Png,
            RawBytes = new byte[10],
            HasAlpha = false
        };

        int firstNum = builder.RegisterImage("img1", img);
        int secondNum = builder.RegisterImage("img1", img);

        Assert.Equal(firstNum, secondNum);
        Assert.Single(builder.GetObjects());
    }

    [Fact]
    public void ImageResourceBuilder_RegisterPngWithAlpha_CreatesSmask()
    {
        var counter = new ObjectCounter();
        var xref = new XRefTable();
        var builder = new ImageResourceBuilder(counter, xref, compress: false);

        var img = new ImageData
        {
            PixelWidth = 10,
            PixelHeight = 10,
            Format = ImageFormat.Png,
            RawBytes = new byte[10],
            HasAlpha = true,
            AlphaBytes = new byte[10]
        };

        int mainNum = builder.RegisterImage("imgAlpha", img);
        var objects = builder.GetObjects();

        // Should have created 2 PDF objects: the Smask and the Image itself
        Assert.Equal(2, objects.Count);
        
        string dict = builder.BuildImageDict();
        Assert.Contains("/imgAlpha", dict);
    }

    [Fact]
    public void ImageResourceBuilder_RegisterJpeg_CreatesJpegObject()
    {
        var counter = new ObjectCounter();
        var xref = new XRefTable();
        var builder = new ImageResourceBuilder(counter, xref, compress: false);

        var img = new ImageData
        {
            PixelWidth = 20,
            PixelHeight = 20,
            Format = ImageFormat.Jpeg,
            RawBytes = new byte[20],
            HasAlpha = false
        };

        int mainNum = builder.RegisterImage("imgJpeg", img);
        var objects = builder.GetObjects();

        Assert.Single(objects);
        using var ms = new MemoryStream();
        objects[0].WriteTo(ms);
        string serialized = Encoding.Latin1.GetString(ms.ToArray());
        Assert.Contains("/DCTDecode", serialized);
    }

    [Fact]
    public void ImageResourceBuilder_RegisterPngWithAlphaButNoAlphaBytes_ThrowsArgumentException()
    {
        var counter = new ObjectCounter();
        var xref = new XRefTable();
        var builder = new ImageResourceBuilder(counter, xref, compress: false);

        var img = new ImageData
        {
            PixelWidth = 10,
            PixelHeight = 10,
            Format = ImageFormat.Png,
            RawBytes = new byte[10],
            HasAlpha = true,
            AlphaBytes = null // missing alpha bytes
        };

        Assert.Throws<ArgumentException>(() => builder.RegisterImage("imgBadAlpha", img));
    }
}

/// <summary>
/// Malicious PNG input: header dimensions, chunk lengths and compressed data come from
/// the image and must not drive unbounded allocation or int overflow.
/// </summary>
public class PngDecoderHardeningTests
{
    private static byte[] Chunk(string type, byte[] data, int? declaredLength = null)
    {
        byte[] chunk = new byte[12 + data.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(chunk, declaredLength ?? data.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(chunk, 4);
        data.CopyTo(chunk, 8);
        return chunk; // CRC left as zero; the decoder does not verify it
    }

    private static byte[] Ihdr(int width, int height, byte colorType = 2)
    {
        byte[] d = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(d, width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(d.AsSpan(4), height);
        d[8] = 8;          // bit depth
        d[9] = colorType;  // 2 = RGB
        return Chunk("IHDR", d);
    }

    private static byte[] Zlib(byte[] raw)
    {
        using MemoryStream ms = new();
        using (System.IO.Compression.ZLibStream z = new(ms, System.IO.Compression.CompressionLevel.SmallestSize))
            z.Write(raw);
        return ms.ToArray();
    }

    private static byte[] Png(params byte[][] chunks)
        => [.. Constants.PngSignature, .. chunks.SelectMany(c => c), .. Chunk("IEND", [])];

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(100_000, 100_000)]  // 10 gigapixels: int overflow and multi-GB allocation
    public void InvalidOrHugeDimensions_ThrowInvalidDataException(int width, int height)
    {
        byte[] png = Png(Ihdr(width, height), Chunk("IDAT", Zlib(new byte[4])));

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(png));
    }

    [Fact]
    public void DecompressionBomb_DoesNotInflateBeyondDeclaredImageSize()
    {
        // 1x1 RGB needs 4 bytes (filter byte + 3 samples); the IDAT inflates to 64 MB of zeros.
        byte[] png = Png(Ihdr(1, 1), Chunk("IDAT", Zlib(new byte[64 * 1024 * 1024])));

        long before = GC.GetAllocatedBytesForCurrentThread();
        ImageData image = PngDecoder.Decode(png);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(1, image.PixelWidth);
        Assert.True(allocated < 8 * 1024 * 1024, $"Decoding allocated {allocated:N0} bytes.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void ChunkWithInvalidDeclaredLength_ThrowsInvalidDataException(int declaredLength)
    {
        byte[] png = Png(Ihdr(1, 1), Chunk("IDAT", new byte[8], declaredLength));

        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(png));
    }

    [Fact]
    public void ValidSmallPng_StillDecodes()
    {
        byte[] png = Png(Ihdr(2, 1), Chunk("IDAT", Zlib([0, 255, 0, 0, 0, 0, 255])));

        ImageData image = PngDecoder.Decode(png);

        Assert.Equal(2, image.PixelWidth);
        Assert.Equal(new byte[] { 255, 0, 0, 0, 0, 255 }, image.RawBytes);
    }
}
