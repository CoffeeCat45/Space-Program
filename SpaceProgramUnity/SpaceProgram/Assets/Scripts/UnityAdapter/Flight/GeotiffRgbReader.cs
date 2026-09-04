using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

/// <summary>
/// Minimal RGB GeoTIFF reader for NASA Blue Marble color maps.
/// Supports uncompressed (1) and zlib/deflate (8) strip compression.
/// North is row 0 in the file; output pixels use canonical geographic layout
/// (south pole row 0 in the Texture2D, north pole at the top).
/// </summary>
public static class GeotiffRgbReader
{
    const ushort TagImageWidth = 256;
    const ushort TagImageLength = 257;
    const ushort TagBitsPerSample = 258;
    const ushort TagCompression = 259;
    const ushort TagStripOffsets = 273;
    const ushort TagRowsPerStrip = 278;
    const ushort TagStripByteCounts = 279;
    const ushort TagSamplesPerPixel = 277;

    const int CompressionNone = 1;
    const int CompressionZlibDeflate = 8;

    public readonly struct Raster
    {
        public Raster(int width, int height, Color32[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public int Width { get; }

        public int Height { get; }

        public Color32[] Pixels { get; }
    }

    public static Raster ReadRgb(string absolutePath, int maxWidth = 0, Action<float> reportProgress = null)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            throw new ArgumentException("Path is required.", nameof(absolutePath));
        }

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("GeoTIFF not found.", absolutePath);
        }

        byte[] fileBytes = File.ReadAllBytes(absolutePath);
        return ReadRgb(fileBytes, absolutePath, maxWidth, reportProgress);
    }

    public static Raster ReadRgb(byte[] fileBytes, string debugName, int maxWidth = 0, Action<float> reportProgress = null)
    {
        if (fileBytes == null || fileBytes.Length < 8)
        {
            throw new InvalidDataException(debugName + ": file too small to be a TIFF.");
        }

        bool littleEndian = fileBytes[0] == 0x49 && fileBytes[1] == 0x49;
        bool bigEndian = fileBytes[0] == 0x4D && fileBytes[1] == 0x4D;

        if (!littleEndian && !bigEndian)
        {
            throw new InvalidDataException(debugName + ": invalid TIFF byte order marker.");
        }

        int ifdOffset = ReadInt32(fileBytes, 4, littleEndian);
        int width = 0;
        int height = 0;
        int bitsPerSample = 0;
        int samplesPerPixel = 1;
        int compression = 1;
        int rowsPerStrip = 0;
        int[] stripOffsets = null;
        int[] stripByteCounts = null;

        int entryCount = ReadUInt16(fileBytes, ifdOffset, littleEndian);

        for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            int entryOffset = ifdOffset + 2 + (entryIndex * 12);
            int tag = ReadUInt16(fileBytes, entryOffset, littleEndian);
            int type = ReadUInt16(fileBytes, entryOffset + 2, littleEndian);
            int count = ReadInt32(fileBytes, entryOffset + 4, littleEndian);
            int valueOrOffset = ReadInt32(fileBytes, entryOffset + 8, littleEndian);

            switch (tag)
            {
                case TagImageWidth:
                    width = ReadTagScalarInt(fileBytes, type, count, valueOrOffset, littleEndian);
                    break;
                case TagImageLength:
                    height = ReadTagScalarInt(fileBytes, type, count, valueOrOffset, littleEndian);
                    break;
                case TagBitsPerSample:
                    bitsPerSample = ReadTagFirstUInt16(
                        fileBytes,
                        type,
                        count,
                        valueOrOffset,
                        littleEndian
                    );
                    break;
                case TagSamplesPerPixel:
                    samplesPerPixel = ReadTagScalarInt(
                        fileBytes,
                        type,
                        count,
                        valueOrOffset,
                        littleEndian
                    );
                    break;
                case TagCompression:
                    compression = ReadTagScalarInt(
                        fileBytes,
                        type,
                        count,
                        valueOrOffset,
                        littleEndian
                    );
                    break;
                case TagStripOffsets:
                    stripOffsets = ReadTagIntArray(
                        fileBytes,
                        type,
                        count,
                        valueOrOffset,
                        littleEndian
                    );
                    break;
                case TagRowsPerStrip:
                    rowsPerStrip = ReadTagScalarInt(
                        fileBytes,
                        type,
                        count,
                        valueOrOffset,
                        littleEndian
                    );
                    break;
                case TagStripByteCounts:
                    stripByteCounts = ReadTagIntArray(
                        fileBytes,
                        type,
                        count,
                        valueOrOffset,
                        littleEndian
                    );
                    break;
            }
        }

        if (width <= 0 || height <= 0 || stripOffsets == null || stripOffsets.Length == 0)
        {
            throw new InvalidDataException(debugName + ": missing required TIFF tags.");
        }

        if (compression != CompressionNone && compression != CompressionZlibDeflate)
        {
            throw new NotSupportedException(
                debugName + ": unsupported TIFF compression=" + compression +
                " (supported: 1=none, 8=zlib/deflate)."
            );
        }

        if (bitsPerSample != 8 || samplesPerPixel < 3)
        {
            throw new NotSupportedException(
                debugName + ": expected 8-bit RGB TIFF, got bits=" + bitsPerSample +
                " samples=" + samplesPerPixel + "."
            );
        }

        if (rowsPerStrip <= 0)
        {
            rowsPerStrip = height;
        }

        int bytesPerPixel = samplesPerPixel;

        if (stripByteCounts == null || stripByteCounts.Length == 0)
        {
            stripByteCounts = new int[] { width * rowsPerStrip * bytesPerPixel };
        }

        bool downscaleDuringRead = maxWidth > 0 && width > maxWidth;
        int outputWidth = downscaleDuringRead ? maxWidth : width;
        int outputHeight = downscaleDuringRead ?
            Math.Max(1, (int)Math.Round(height * (maxWidth / (double)width))) :
            height;

        var pixels = new Color32[outputWidth * outputHeight];
        int stripIndex = 0;
        int sourceRow = 0;

        while (sourceRow < height)
        {
            int stripRows = Math.Min(rowsPerStrip, height - sourceRow);
            int stripOffset = stripOffsets[stripIndex];
            int stripByteCount = GetStripByteCount(stripByteCounts, stripIndex);
            byte[] stripBytes = ReadStripBytes(
                fileBytes,
                compression,
                stripOffset,
                stripByteCount,
                width,
                stripRows,
                bytesPerPixel,
                debugName,
                stripIndex
            );

            int byteIndex = 0;

            for (int rowInStrip = 0; rowInStrip < stripRows; rowInStrip++)
            {
                int fileRow = sourceRow + rowInStrip;
                int canonicalRow = (height - 1) - fileRow;
                int outputRow = downscaleDuringRead ?
                    Mathf.RoundToInt(canonicalRow * (outputHeight - 1) / (float)(height - 1)) :
                    canonicalRow;
                int destinationRowOffset = outputRow * outputWidth;

                for (int column = 0; column < width; column++)
                {
                    byte red = stripBytes[byteIndex++];
                    byte green = stripBytes[byteIndex++];
                    byte blue = stripBytes[byteIndex++];

                    if (samplesPerPixel > 3)
                    {
                        byteIndex += samplesPerPixel - 3;
                    }

                    int outputColumn = downscaleDuringRead ?
                        Mathf.RoundToInt(column * (outputWidth - 1) / (float)(width - 1)) :
                        column;
                    int pixelIndex = destinationRowOffset + outputColumn;
                    pixels[pixelIndex] = new Color32(red, green, blue, 255);
                }
            }

            sourceRow += stripRows;
            stripIndex++;
            reportProgress?.Invoke(Mathf.Clamp01(sourceRow / (float)height));
        }

        return new Raster(outputWidth, outputHeight, pixels);
    }

    static int GetStripByteCount(int[] stripByteCounts, int stripIndex)
    {
        if (stripByteCounts.Length == 1)
        {
            return stripByteCounts[0];
        }

        return stripByteCounts[stripIndex];
    }

    static byte[] ReadStripBytes(
        byte[] fileBytes,
        int compression,
        int stripOffset,
        int stripByteCount,
        int width,
        int stripRows,
        int bytesPerPixel,
        string debugName,
        int stripIndex
    )
    {
        int expectedUncompressedBytes = width * stripRows * bytesPerPixel;

        if (compression == CompressionNone)
        {
            if (stripByteCount != expectedUncompressedBytes)
            {
                throw new InvalidDataException(
                    debugName + ": strip " + stripIndex + " byte count mismatch."
                );
            }

            var uncompressed = new byte[expectedUncompressedBytes];
            Buffer.BlockCopy(
                fileBytes,
                stripOffset,
                uncompressed,
                0,
                expectedUncompressedBytes
            );
            return uncompressed;
        }

        byte[] compressed = new byte[stripByteCount];
        Buffer.BlockCopy(fileBytes, stripOffset, compressed, 0, stripByteCount);
        byte[] decompressed = DecompressZlib(compressed);

        if (decompressed.Length != expectedUncompressedBytes)
        {
            throw new InvalidDataException(
                debugName + ": strip " + stripIndex + " decompressed to " +
                decompressed.Length + " bytes, expected " + expectedUncompressedBytes + "."
            );
        }

        return decompressed;
    }

    /// <summary>TIFF compression tag 8 stores zlib-wrapped deflate per strip.</summary>
    static byte[] DecompressZlib(byte[] compressed)
    {
        using var compressedStream = new MemoryStream(compressed, false);
        compressedStream.ReadByte();
        compressedStream.ReadByte();

        using var deflate = new DeflateStream(
            compressedStream,
            CompressionMode.Decompress,
            leaveOpen: true
        );
        using var output = new MemoryStream(compressed.Length * 8);
        deflate.CopyTo(output);
        return output.ToArray();
    }

    public static Texture2D ToTexture(Raster raster, string textureName)
    {
        var texture = new Texture2D(raster.Width, raster.Height, TextureFormat.RGB24, false)
        {
            name = textureName,
        };
        texture.SetPixels32(raster.Pixels);
        texture.Apply();
        return texture;
    }

    static int ReadTagScalarInt(
        byte[] fileBytes,
        int type,
        int count,
        int valueOrOffset,
        bool littleEndian
    )
    {
        if (count != 1)
        {
            throw new NotSupportedException("Expected scalar TIFF tag.");
        }

        return type switch
        {
            3 => ReadTagFirstUInt16(fileBytes, type, count, valueOrOffset, littleEndian),
            4 => valueOrOffset,
            _ => throw new NotSupportedException("Unsupported TIFF tag type " + type + "."),
        };
    }

    static int ReadTagFirstUInt16(
        byte[] fileBytes,
        int type,
        int count,
        int valueOrOffset,
        bool littleEndian
    )
    {
        if (count <= 0)
        {
            return 0;
        }

        if (count == 1 && type == 3)
        {
            return valueOrOffset & 0xFFFF;
        }

        int[] values = ReadTagIntArray(fileBytes, type, count, valueOrOffset, littleEndian);
        return values[0];
    }

    static int[] ReadTagIntArray(
        byte[] fileBytes,
        int type,
        int count,
        int valueOrOffset,
        bool littleEndian
    )
    {
        var values = new int[count];

        if (count == 0)
        {
            return values;
        }

        if (type == 3 && count == 1)
        {
            values[0] = valueOrOffset & 0xFFFF;
            return values;
        }

        if ((type == 3 && count <= 2) || (type == 4 && count == 1))
        {
            int packedOffset = valueOrOffset;

            for (int index = 0; index < count; index++)
            {
                if (type == 3)
                {
                    int shift = index * 16;
                    values[index] = (packedOffset >> shift) & 0xFFFF;
                }
                else
                {
                    values[index] = packedOffset;
                }
            }

            return values;
        }

        int dataOffset = valueOrOffset;

        for (int index = 0; index < count; index++)
        {
            values[index] = type switch
            {
                3 => ReadUInt16(fileBytes, dataOffset + (index * 2), littleEndian),
                4 => ReadInt32(fileBytes, dataOffset + (index * 4), littleEndian),
                _ => throw new NotSupportedException("Unsupported TIFF array type " + type + "."),
            };
        }

        return values;
    }

    static int ReadInt32(byte[] fileBytes, int offset, bool littleEndian)
    {
        if (littleEndian)
        {
            return fileBytes[offset] |
                (fileBytes[offset + 1] << 8) |
                (fileBytes[offset + 2] << 16) |
                (fileBytes[offset + 3] << 24);
        }

        return (fileBytes[offset] << 24) |
            (fileBytes[offset + 1] << 16) |
            (fileBytes[offset + 2] << 8) |
            fileBytes[offset + 3];
    }

    static int ReadUInt16(byte[] fileBytes, int offset, bool littleEndian)
    {
        if (littleEndian)
        {
            return fileBytes[offset] | (fileBytes[offset + 1] << 8);
        }

        return (fileBytes[offset] << 8) | fileBytes[offset + 1];
    }
}
