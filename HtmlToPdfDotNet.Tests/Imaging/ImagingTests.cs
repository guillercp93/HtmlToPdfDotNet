using HtmlToPdfDotNet.Library.Commons;
using HtmlToPdfDotNet.Library.Models.Imaging;
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
}
