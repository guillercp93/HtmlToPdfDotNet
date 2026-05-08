using System.Buffers.Binary;
using System.Text;

namespace HtmlToPdfDotNet.Library.Commons;

/// <summary>
/// Chunk reader for PNG images.
/// </summary>
public sealed class ChunkReader
{
    private int _pos = 0;
    private byte[] _data;

    /// <summary>
    /// Creates a new <see cref="ChunkReader"/>.
    /// </summary>
    /// <param name="data">The PNG data to read from.</param>
    /// <param name="offset">The offset to start reading from.</param>
    public ChunkReader(byte[]? data, int offset)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _pos = offset;
    }

    /// <summary>
    /// Reads the next chunk from the PNG data.
    /// </summary>
    /// <returns>A chunk of data from the PNG file.</returns>
    public Chunk ReadChunk()
    {
        if (_pos + 8 > _data.Length)
        {
            throw new InvalidDataException("PNG: File truncated.");
        }

        // 4 bytes length
        int length = BinaryPrimitives.ReadInt32BigEndian(_data.AsSpan(_pos, 4));
        _pos += 4;

        // 4 bytes type (ASCII)
        string type = Encoding.ASCII.GetString(_data, _pos, 4);
        _pos += 4;

        // payload bytes
        if (_pos + length > _data.Length)
        {
            throw new InvalidDataException($"PNG: chunk {type} data truncated.");
        }
        byte[] chunkData = _data[_pos..(_pos + length)];
        _pos += length;

        _pos += 4; // skip CRC

        return new Chunk(type, chunkData);
    }
}

/// <summary>
/// Represents a chunk of data in a PNG file.
/// </summary>
public readonly struct Chunk
{
    /// <summary>
    /// Gets the type of the chunk (4 bytes, ASCII).
    /// </summary>
    public string Type { get; }
    /// <summary>
    /// Gets the data of the chunk.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Creates a new <see cref="Chunk"/>.
    /// </summary>
    public Chunk(string type, byte[] data)
    {
        Type = type;
        Data = data;
    }
}