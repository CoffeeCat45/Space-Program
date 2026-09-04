using System;
using System.IO;

/// <summary>
/// Deterministic on-disk elevation grid baked from GEBCO GeoTIFF (Editor only).
/// Little-endian, float32 meters, row-major with V=0 at the first row (south pole).
/// </summary>
public static class PlanetElevationBinaryFormat
{
    public const uint CurrentVersion = 1;
    public const int HeaderByteCount = 32;

    public static readonly byte[] MagicBytes = { (byte)'S', (byte)'P', (byte)'E', (byte)'L' };

    public enum StorageFormat : uint
    {
        Float32Meters = 1,
    }

    public readonly struct Header
    {
        public Header(uint version, int width, int height, StorageFormat storageFormat)
        {
            Version = version;
            Width = width;
            Height = height;
            StorageFormat = storageFormat;
        }

        public uint Version { get; }

        public int Width { get; }

        public int Height { get; }

        public StorageFormat StorageFormat { get; }

        public int PixelCount => Width * Height;

        public int PayloadByteCount =>
            StorageFormat == StorageFormat.Float32Meters ?
                PixelCount * sizeof(float) :
                0;
    }

    public static void Write(Stream stream, float[] elevationsMeters, int width, int height)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (elevationsMeters == null)
        {
            throw new ArgumentNullException(nameof(elevationsMeters));
        }

        if (elevationsMeters.Length != width * height)
        {
            throw new ArgumentException(
                "Elevation array length must equal width * height.",
                nameof(elevationsMeters)
            );
        }

        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(MagicBytes);
        writer.Write(CurrentVersion);
        writer.Write(width);
        writer.Write(height);
        writer.Write((uint)StorageFormat.Float32Meters);
        writer.Write(0u);

        for (int i = 0; i < elevationsMeters.Length; i++)
        {
            writer.Write(elevationsMeters[i]);
        }
    }

    public static Header ReadHeader(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        byte[] magic = reader.ReadBytes(MagicBytes.Length);

        for (int i = 0; i < MagicBytes.Length; i++)
        {
            if (magic[i] != MagicBytes[i])
            {
                throw new InvalidDataException("Planet elevation file has invalid SPEL magic header.");
            }
        }

        uint version = reader.ReadUInt32();

        if (version != CurrentVersion)
        {
            throw new InvalidDataException(
                "Unsupported planet elevation version " + version + " (expected " + CurrentVersion + ")."
            );
        }

        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        var storageFormat = (StorageFormat)reader.ReadUInt32();
        reader.ReadUInt32();

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("Planet elevation header has invalid dimensions.");
        }

        if (storageFormat != StorageFormat.Float32Meters)
        {
            throw new InvalidDataException(
                "Unsupported planet elevation storage format " + storageFormat + "."
            );
        }

        return new Header(version, width, height, storageFormat);
    }

    public static float[] ReadPayload(Stream stream, Header header)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        int byteCount = header.PayloadByteCount;
        byte[] bytes = new byte[byteCount];
        int offset = 0;

        while (offset < byteCount)
        {
            int read = stream.Read(bytes, offset, byteCount - offset);

            if (read <= 0)
            {
                throw new EndOfStreamException("Unexpected end of planet elevation payload.");
            }

            offset += read;
        }

        var elevations = new float[header.PixelCount];
        Buffer.BlockCopy(bytes, 0, elevations, 0, byteCount);
        return elevations;
    }
}
