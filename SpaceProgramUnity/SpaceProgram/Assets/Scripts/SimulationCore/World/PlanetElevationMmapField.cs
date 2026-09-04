using System;
using System.IO;
using System.IO.MemoryMappedFiles;

/// <summary>
/// Memory-mapped read-only access to full-resolution Int16 elevation grid (~7.46 GB).
/// Does not load the file into managed RAM.
/// </summary>
public sealed class PlanetElevationMmapField : IDisposable
{
    readonly MemoryMappedFile mappedFile;
    readonly MemoryMappedViewAccessor accessor;
    readonly short[] memorySamples;
    readonly int width;
    readonly int height;
    bool disposed;

    PlanetElevationMmapField(
        MemoryMappedFile mappedFile,
        MemoryMappedViewAccessor accessor,
        short[] memorySamples,
        int width,
        int height
    )
    {
        this.mappedFile = mappedFile;
        this.accessor = accessor;
        this.memorySamples = memorySamples;
        this.width = width;
        this.height = height;
    }

    public int Width => width;

    public int Height => height;

    public static PlanetElevationMmapField OpenFile(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            throw new ArgumentException("Path is required.", nameof(absolutePath));
        }

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("Elevation raw file was not found.", absolutePath);
        }

        long fileLength = new FileInfo(absolutePath).Length;

        if (fileLength < PlanetElevationRawFormat.ExpectedByteCount)
        {
            throw new InvalidDataException(
                "Elevation raw file is too small. Expected at least " +
                PlanetElevationRawFormat.ExpectedByteCount +
                " bytes, got " + fileLength + "."
            );
        }

        return OpenMappedFile(
            absolutePath,
            PlanetElevationRawFormat.Width,
            PlanetElevationRawFormat.Height,
            PlanetElevationRawFormat.ExpectedByteCount
        );
    }

    /// <summary>Arbitrary Int16 row-major grid (Moon LDEM, tests).</summary>
    public static PlanetElevationMmapField OpenFile(
        string absolutePath,
        int gridWidth,
        int gridHeight
    )
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            throw new ArgumentException("Path is required.", nameof(absolutePath));
        }

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("Elevation raw file was not found.", absolutePath);
        }

        if (gridWidth <= 0 || gridHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                "Grid size must be positive.",
                gridWidth + "x" + gridHeight
            );
        }

        long expectedByteCount = (long)gridWidth * gridHeight * PlanetElevationRawFormat.BytesPerSample;
        long fileLength = new FileInfo(absolutePath).Length;

        if (fileLength < expectedByteCount)
        {
            throw new InvalidDataException(
                "Elevation raw file is too small. Expected at least " +
                expectedByteCount +
                " bytes, got " + fileLength + "."
            );
        }

        return OpenMappedFile(absolutePath, gridWidth, gridHeight, expectedByteCount);
    }

    /// <summary>Opens a smaller raw grid for tests (width × height Int16, row-major).</summary>
    public static PlanetElevationMmapField OpenFromInt16Grid(short[] elevations, int gridWidth, int gridHeight)
    {
        if (elevations == null)
        {
            throw new ArgumentNullException(nameof(elevations));
        }

        if (elevations.Length != gridWidth * gridHeight)
        {
            throw new ArgumentException("Elevation array length must equal width * height.");
        }

        // In-memory copy avoids MemoryMappedFile.CreateNew, which can hard-crash Unity Test Runner.
        var memorySamples = new short[elevations.Length];
        Array.Copy(elevations, memorySamples, elevations.Length);
        return new PlanetElevationMmapField(null, null, memorySamples, gridWidth, gridHeight);
    }

    public short ReadSampleMeters(int x, int y)
    {
        ThrowIfDisposed();
        x = WrapX(x);
        y = ClampY(y);

        if (memorySamples != null)
        {
            return memorySamples[(y * width) + x];
        }

        long offset = ((long)y * width + x) * PlanetElevationRawFormat.BytesPerSample;
        return accessor.ReadInt16(offset);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        accessor?.Dispose();
        mappedFile?.Dispose();
    }

    static PlanetElevationMmapField OpenMappedFile(
        string absolutePath,
        int gridWidth,
        int gridHeight,
        long viewByteCount
    )
    {
        MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(
            absolutePath,
            FileMode.Open,
            mapName: null,
            capacity: 0,
            MemoryMappedFileAccess.Read
        );
        MemoryMappedViewAccessor accessor = mappedFile.CreateViewAccessor(
            0,
            viewByteCount,
            MemoryMappedFileAccess.Read
        );

        return new PlanetElevationMmapField(mappedFile, accessor, null, gridWidth, gridHeight);
    }

    int WrapX(int x)
    {
        int wrapped = x % width;

        if (wrapped < 0)
        {
            wrapped += width;
        }

        return wrapped;
    }

    int ClampY(int y)
    {
        if (y < 0)
        {
            return 0;
        }

        if (y >= height)
        {
            return height - 1;
        }

        return y;
    }

    void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(PlanetElevationMmapField));
        }
    }
}
