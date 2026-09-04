using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Minimal GeoTIFF elevation reader for Editor bake only (uncompressed strip TIFF).
/// Supports UInt8, Int16, and Float32 samples with one or more strips.
/// </summary>
static class GeotiffElevationReader
{
    const ushort TagImageWidth = 256;
    const ushort TagImageLength = 257;
    const ushort TagBitsPerSample = 258;
    const ushort TagCompression = 259;
    const ushort TagStripOffsets = 273;
    const ushort TagRowsPerStrip = 278;
    const ushort TagStripByteCounts = 279;
    const ushort TagSampleFormat = 339;

    const ushort SampleFormatUnsignedInt = 1;
    const ushort SampleFormatSignedInt = 2;
    const ushort SampleFormatIeeeFloat = 3;

    public readonly struct Raster
    {
        public Raster(int width, int height, float[] elevationsMeters)
        {
            Width = width;
            Height = height;
            ElevationsMeters = elevationsMeters;
        }

        public int Width { get; }

        public int Height { get; }

        public float[] ElevationsMeters { get; }
    }

    public static Raster ReadElevationMeters(string absolutePath)
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
        return ReadElevationMeters(fileBytes, absolutePath);
    }

    public static Raster ReadElevationMeters(byte[] fileBytes, string debugName = "GeoTIFF")
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
        int sampleFormat = SampleFormatUnsignedInt;
        int compression = 1;
        int rowsPerStrip = 0;
        int[] stripOffsets = null;
        int[] stripByteCounts = null;

        int entryCount = ReadUInt16(fileBytes, ifdOffset, littleEndian);

        for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            int entryOffset = ifdOffset + 2 + (entryIndex * 12);
            ushort tag = ReadUInt16(fileBytes, entryOffset, littleEndian);
            ushort type = ReadUInt16(fileBytes, entryOffset + 2, littleEndian);
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
                case TagSampleFormat:
                    sampleFormat = ReadTagFirstUInt16(
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

        if (compression != 1)
        {
            throw new NotSupportedException(
                debugName + ": only uncompressed TIFF (compression=1) is supported in this baker. " +
                "Re-export the GEBCO file without compression or use an external tool."
            );
        }

        if (rowsPerStrip <= 0)
        {
            rowsPerStrip = height;
        }

        if (stripByteCounts == null || stripByteCounts.Length == 0)
        {
            int bytesPerPixel = Math.Max(1, bitsPerSample / 8);
            stripByteCounts = new int[] { width * rowsPerStrip * bytesPerPixel };
        }

        bool isUInt8 = bitsPerSample == 8 && sampleFormat == SampleFormatUnsignedInt;
        bool isInt16 = bitsPerSample == 16 && sampleFormat == SampleFormatSignedInt;
        bool isFloat32 = bitsPerSample == 32 && sampleFormat == SampleFormatIeeeFloat;

        if (!isUInt8 && !isInt16 && !isFloat32)
        {
            throw new NotSupportedException(
                debugName + ": expected UInt8 (format=1), Int16 (format=2), or Float32 (format=3) " +
                "elevation samples, got bits=" + bitsPerSample + " format=" + sampleFormat + "."
            );
        }

        int bytesPerSample = bitsPerSample / 8;
        ValidateStrips(debugName, width, height, rowsPerStrip, bytesPerSample, stripOffsets, stripByteCounts);

        var elevations = new float[width * height];
        ReadStripRaster(
            fileBytes,
            littleEndian,
            width,
            height,
            rowsPerStrip,
            bytesPerSample,
            isUInt8,
            isInt16,
            stripOffsets,
            stripByteCounts,
            elevations
        );

        if (isUInt8)
        {
            Debug.LogWarning(
                "GeotiffElevationReader: " + debugName +
                " is 8-bit UInt elevation (0 = sea level, no bathymetry). " +
                "Decoded with ~" + PlanetSurfaceConstants.EarthrevUInt8MetersPerSample.ToString("F2") +
                " m per DN. For full GEBCO bathymetry, use an Int16 or Float32 GeoTIFF source."
            );
        }

        return new Raster(width, height, elevations);
    }

    static void ValidateStrips(
        string debugName,
        int width,
        int height,
        int rowsPerStrip,
        int bytesPerSample,
        int[] stripOffsets,
        int[] stripByteCounts
    )
    {
        int expectedStripCount = (height + rowsPerStrip - 1) / rowsPerStrip;

        if (stripOffsets.Length != expectedStripCount)
        {
            throw new NotSupportedException(
                debugName + ": unexpected strip count (got " + stripOffsets.Length +
                ", expected " + expectedStripCount + ")."
            );
        }

        if (stripByteCounts.Length != 1 && stripByteCounts.Length != expectedStripCount)
        {
            throw new NotSupportedException(
                debugName + ": strip byte count array length mismatch."
            );
        }

        int uniformStripBytes = stripByteCounts.Length == 1 ? stripByteCounts[0] : -1;
        int expectedStripBytes = width * rowsPerStrip * bytesPerSample;

        for (int stripIndex = 0; stripIndex < expectedStripCount; stripIndex++)
        {
            int stripRows = Math.Min(rowsPerStrip, height - (stripIndex * rowsPerStrip));
            int expectedBytes = width * stripRows * bytesPerSample;
            int actualBytes = stripByteCounts.Length == 1 ?
                uniformStripBytes :
                stripByteCounts[stripIndex];

            if (actualBytes != expectedBytes)
            {
                throw new NotSupportedException(
                    debugName + ": strip " + stripIndex + " byte count mismatch (got " +
                    actualBytes + ", expected " + expectedBytes + ")."
                );
            }
        }
    }

    static void ReadStripRaster(
        byte[] fileBytes,
        bool littleEndian,
        int width,
        int height,
        int rowsPerStrip,
        int bytesPerSample,
        bool isUInt8,
        bool isInt16,
        int[] stripOffsets,
        int[] stripByteCounts,
        float[] destination
    )
    {
        int stripIndex = 0;
        int sourceRow = 0;

        while (sourceRow < height)
        {
            int stripRows = Math.Min(rowsPerStrip, height - sourceRow);
            int stripOffset = stripOffsets[stripIndex];
            int byteIndex = stripOffset;

            for (int rowInStrip = 0; rowInStrip < stripRows; rowInStrip++)
            {
                int destinationRow = (height - 1) - sourceRow;
                int destinationRowOffset = destinationRow * width;

                for (int column = 0; column < width; column++)
                {
                    float meters = isUInt8 ?
                        DecodeUInt8Sample(fileBytes[byteIndex]) :
                        isInt16 ?
                            ReadInt16(fileBytes, byteIndex, littleEndian) :
                            ReadFloat32(fileBytes, byteIndex, littleEndian);

                    destination[destinationRowOffset + column] = meters;
                    byteIndex += bytesPerSample;
                }

                sourceRow++;
            }

            stripIndex++;
        }
    }

    /// <summary>
    /// Earthrev 8-bit exports use 0 for ocean/sea level and ~42 m per DN up to ~10.7 km.
    /// </summary>
    static float DecodeUInt8Sample(byte sample)
    {
        return sample * PlanetSurfaceConstants.EarthrevUInt8MetersPerSample;
    }

    static int ReadTagScalarInt(
        byte[] fileBytes,
        ushort type,
        int count,
        int valueOrOffset,
        bool littleEndian
    )
    {
        if (count != 1)
        {
            throw new NotSupportedException("Expected a single-value TIFF tag.");
        }

        switch (type)
        {
            case 3:
                return ReadTagFirstUInt16(fileBytes, type, count, valueOrOffset, littleEndian);
            case 4:
            case 13:
                return valueOrOffset;
            default:
                throw new NotSupportedException("Unsupported TIFF scalar tag type " + type + ".");
        }
    }

    static int ReadTagFirstUInt16(
        byte[] fileBytes,
        ushort type,
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
        ushort type,
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
            switch (type)
            {
                case 3:
                    values[index] = ReadUInt16(
                        fileBytes,
                        dataOffset + (index * 2),
                        littleEndian
                    );
                    break;
                case 4:
                case 13:
                    values[index] = ReadInt32(
                        fileBytes,
                        dataOffset + (index * 4),
                        littleEndian
                    );
                    break;
                default:
                    throw new NotSupportedException("Unsupported TIFF array tag type " + type + ".");
            }
        }

        return values;
    }

    static ushort ReadUInt16(byte[] bytes, int offset, bool littleEndian)
    {
        if (littleEndian)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        return (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
    }

    static short ReadInt16(byte[] bytes, int offset, bool littleEndian)
    {
        return (short)ReadUInt16(bytes, offset, littleEndian);
    }

    static int ReadInt32(byte[] bytes, int offset, bool littleEndian)
    {
        if (littleEndian)
        {
            return bytes[offset] |
                (bytes[offset + 1] << 8) |
                (bytes[offset + 2] << 16) |
                (bytes[offset + 3] << 24);
        }

        return (bytes[offset] << 24) |
            (bytes[offset + 1] << 16) |
            (bytes[offset + 2] << 8) |
            bytes[offset + 3];
    }

    static float ReadFloat32(byte[] bytes, int offset, bool littleEndian)
    {
        byte[] temp = littleEndian ?
            new byte[] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] } :
            new byte[] { bytes[offset + 3], bytes[offset + 2], bytes[offset + 1], bytes[offset] };
        return BitConverter.ToSingle(temp, 0);
    }
}
