using System.Diagnostics;
using BitMiracle.LibTiff.Classic;

namespace Gebco2026RawStitcher;

internal static class Program
{
    const int TileSize = 21600;
    const int GlobalWidth = 86400;
    const int GlobalHeight = 43200;
    const int HemisphereRows = 21600;
    const int BytesPerRow = GlobalWidth * sizeof(short);

    static readonly string[] NorthTileFileNames =
    {
        "gebco_2026_sub_ice_n90.0_s0.0_w-180.0_e-90.0_geotiff.tif",
        "gebco_2026_sub_ice_n90.0_s0.0_w-90.0_e0.0_geotiff.tif",
        "gebco_2026_sub_ice_n90.0_s0.0_w0.0_e90.0_geotiff.tif",
        "gebco_2026_sub_ice_n90.0_s0.0_w90.0_e180.0_geotiff.tif",
    };

    static readonly string[] SouthTileFileNames =
    {
        "gebco_2026_sub_ice_n0.0_s-90.0_w-180.0_e-90.0_geotiff.tif",
        "gebco_2026_sub_ice_n0.0_s-90.0_w-90.0_e0.0_geotiff.tif",
        "gebco_2026_sub_ice_n0.0_s-90.0_w0.0_e90.0_geotiff.tif",
        "gebco_2026_sub_ice_n0.0_s-90.0_w90.0_e180.0_geotiff.tif",
    };

    static int Main(string[] args)
    {
        string repoRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")
        );
        string defaultInput = Path.Combine(
            repoRoot,
            "SpaceProgramUnity",
            "SpaceProgram",
            "Assets",
            "StreamingAssets",
            "MapMode",
            "gebco_2026_sub_ice_topo_geotiff"
        );
        string defaultOutput = Path.Combine(
            repoRoot,
            "SpaceProgramUnity",
            "SpaceProgram",
            "Assets",
            "StreamingAssets",
            "Planet",
            "earth_elevation.raw"
        );

        string inputDirectory = args.Length > 0 ? args[0] : defaultInput;
        string outputPath = args.Length > 1 ? args[1] : defaultOutput;
        bool lodOnly = args.Length > 2 &&
            string.Equals(args[2], "--lod-only", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (lodOnly)
            {
                string planetDirectory = string.IsNullOrEmpty(outputPath) ?
                    Path.GetDirectoryName(defaultOutput) ?? defaultInput :
                    (Directory.Exists(outputPath) ? outputPath : Path.GetDirectoryName(outputPath) ?? outputPath);

                BakeLodOnlyFromTiles(inputDirectory, planetDirectory);
                Console.WriteLine("Done: LOD coarse + mid in " + planetDirectory);
                return 0;
            }

            Stitch(inputDirectory, outputPath);
            Console.WriteLine("Done: " + outputPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Stitch failed: " + ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    static void BakeLodOnlyFromTiles(string inputDirectory, string outputDirectory)
    {
        if (!Directory.Exists(inputDirectory))
        {
            throw new DirectoryNotFoundException("Input directory not found: " + inputDirectory);
        }

        Directory.CreateDirectory(outputDirectory);

        string coarsePath = Path.Combine(outputDirectory, "earth_elevation_coarse_4096.raw");
        string midPath = Path.Combine(outputDirectory, "earth_elevation_mid_8192.raw");
        string highMidPath = Path.Combine(outputDirectory, "earth_elevation_highmid_16384.raw");

        Console.WriteLine("Baking coarse 4096x2048 from tiles...");
        DownsampleFromTiles(
            inputDirectory,
            coarsePath,
            targetWidth: 4096,
            targetHeight: 2048
        );

        Console.WriteLine("Baking mid 8192x4096 from tiles...");
        DownsampleFromTiles(
            inputDirectory,
            midPath,
            targetWidth: 8192,
            targetHeight: 4096
        );

        Console.WriteLine("Baking high-mid 16384x8192 from tiles...");
        DownsampleFromTiles(
            inputDirectory,
            highMidPath,
            targetWidth: 16384,
            targetHeight: 8192
        );
    }

    static void DownsampleFromTiles(
        string inputDirectory,
        string outputPath,
        int targetWidth,
        int targetHeight
    )
    {
        string[] northPaths = ResolveTilePaths(inputDirectory, NorthTileFileNames);
        string[] southPaths = ResolveTilePaths(inputDirectory, SouthTileFileNames);

        using TiffHandleSet northReaders = TiffHandleSet.Open(northPaths);
        using TiffHandleSet southReaders = TiffHandleSet.Open(southPaths);
        byte[] tileScanline = new byte[TileSize * sizeof(short)];
        short[] globalRow = new short[GlobalWidth];
        using FileStream outputStream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

        for (int targetY = 0; targetY < targetHeight; targetY++)
        {
            double sourceY0 = targetY * (GlobalHeight / (double)targetHeight);
            double sourceY1 = (targetY + 1) * (GlobalHeight / (double)targetHeight);
            int yStart = Math.Max(0, (int)Math.Floor(sourceY0));
            int yEnd = Math.Min(GlobalHeight, (int)Math.Ceiling(sourceY1));

            long[] sums = new long[targetWidth];
            int[] counts = new int[targetWidth];

            for (int sourceY = yStart; sourceY < yEnd; sourceY++)
            {
                ReadGlobalRow(
                    northReaders,
                    southReaders,
                    tileScanline,
                    globalRow,
                    sourceY
                );

                for (int targetX = 0; targetX < targetWidth; targetX++)
                {
                    double sourceX0 = targetX * (GlobalWidth / (double)targetWidth);
                    double sourceX1 = (targetX + 1) * (GlobalWidth / (double)targetWidth);
                    int xStart = Math.Max(0, (int)Math.Floor(sourceX0));
                    int xEnd = Math.Min(GlobalWidth, (int)Math.Ceiling(sourceX1));

                    long sum = 0;
                    int count = 0;

                    for (int sourceX = xStart; sourceX < xEnd; sourceX++)
                    {
                        sum += globalRow[sourceX];
                        count++;
                    }

                    sums[targetX] += sum;
                    counts[targetX] += count;
                }
            }

            var targetRow = new short[targetWidth];

            for (int targetX = 0; targetX < targetWidth; targetX++)
            {
                targetRow[targetX] = counts[targetX] > 0 ?
                    (short)Math.Round(sums[targetX] / (double)counts[targetX]) :
                    (short)0;
            }

            byte[] targetRowBytes = new byte[targetWidth * sizeof(short)];
            Buffer.BlockCopy(targetRow, 0, targetRowBytes, 0, targetRowBytes.Length);
            outputStream.Write(targetRowBytes, 0, targetRowBytes.Length);

            if ((targetY + 1) % 256 == 0)
            {
                Console.WriteLine("  target row " + targetY + " / " + (targetHeight - 1));
            }
        }
    }

    static void ReadGlobalRow(
        TiffHandleSet northReaders,
        TiffHandleSet southReaders,
        byte[] tileScanline,
        short[] globalRow,
        int globalY
    )
    {
        TiffHandleSet readers;
        int sourceRow;

        if (globalY < HemisphereRows)
        {
            readers = northReaders;
            sourceRow = globalY;
        }
        else
        {
            readers = southReaders;
            sourceRow = globalY - HemisphereRows;
        }

        int shortOffset = 0;

        for (int tileIndex = 0; tileIndex < readers.Count; tileIndex++)
        {
            if (!readers.Readers[tileIndex].ReadScanline(tileScanline, sourceRow))
            {
                throw new InvalidOperationException(
                    "ReadScanline failed for " + readers.Paths[tileIndex] + " row " + sourceRow + "."
                );
            }

            Buffer.BlockCopy(tileScanline, 0, globalRow, shortOffset * sizeof(short), tileScanline.Length);
            shortOffset += TileSize;
        }
    }

    static void Stitch(string inputDirectory, string outputPath)
    {
        if (!Directory.Exists(inputDirectory))
        {
            throw new DirectoryNotFoundException("Input directory not found: " + inputDirectory);
        }

        string? outputDirectory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        string[] northPaths = ResolveTilePaths(inputDirectory, NorthTileFileNames);
        string[] southPaths = ResolveTilePaths(inputDirectory, SouthTileFileNames);

        var stopwatch = Stopwatch.StartNew();
        byte[] tileScanline = new byte[TileSize * sizeof(short)];
        byte[] mergedRow = new byte[BytesPerRow];

        using FileStream outputStream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

        Console.WriteLine("Stitching northern hemisphere (rows 0.." + (HemisphereRows - 1) + ")...");
        StitchHemisphere(outputStream, northPaths, tileScanline, mergedRow, sourceRowOffset: 0);

        Console.WriteLine("Stitching southern hemisphere (rows " + HemisphereRows + ".." + (GlobalHeight - 1) + ")...");
        StitchHemisphere(outputStream, southPaths, tileScanline, mergedRow, sourceRowOffset: 0);

        stopwatch.Stop();
        long bytesWritten = (long)GlobalWidth * GlobalHeight * sizeof(short);
        Console.WriteLine(
            "Wrote " + bytesWritten + " bytes (" + GlobalWidth + "x" + GlobalHeight + " Int16) in " +
            stopwatch.Elapsed.TotalSeconds.ToString("F1") + " s."
        );
    }

    static void StitchHemisphere(
        Stream outputStream,
        string[] tilePaths,
        byte[] tileScanline,
        byte[] mergedRow,
        int sourceRowOffset
    )
    {
        using TiffHandleSet readers = TiffHandleSet.Open(tilePaths);

        for (int row = 0; row < HemisphereRows; row++)
        {
            int sourceRow = sourceRowOffset + row;
            int writeOffset = 0;

            for (int tileIndex = 0; tileIndex < readers.Count; tileIndex++)
            {
                if (!readers.Readers[tileIndex].ReadScanline(tileScanline, sourceRow))
                {
                    throw new InvalidOperationException(
                        "ReadScanline failed for " + readers.Paths[tileIndex] + " row " + sourceRow + "."
                    );
                }

                Buffer.BlockCopy(tileScanline, 0, mergedRow, writeOffset, tileScanline.Length);
                writeOffset += tileScanline.Length;
            }

            outputStream.Write(mergedRow, 0, mergedRow.Length);

            if ((row + 1) % 1000 == 0)
            {
                Console.WriteLine("  row " + row + " / " + (HemisphereRows - 1));
            }
        }
    }

    static string[] ResolveTilePaths(string inputDirectory, string[] fileNames)
    {
        var paths = new string[fileNames.Length];

        for (int i = 0; i < fileNames.Length; i++)
        {
            string path = Path.Combine(inputDirectory, fileNames[i]);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Missing GEBCO tile.", path);
            }

            paths[i] = path;
        }

        return paths;
    }

    sealed class TiffHandleSet : IDisposable
    {
        public Tiff[] Readers { get; }
        public string[] Paths { get; }

        TiffHandleSet(Tiff[] readers, string[] paths)
        {
            Readers = readers;
            Paths = paths;
        }

        public int Count => Readers.Length;

        public static TiffHandleSet Open(string[] paths)
        {
            var readers = new Tiff[paths.Length];

            for (int i = 0; i < paths.Length; i++)
            {
                readers[i] = Tiff.Open(paths[i], "r") ??
                    throw new InvalidOperationException("LibTiff failed to open " + paths[i] + ".");

                ValidateTile(readers[i], paths[i]);
            }

            return new TiffHandleSet(readers, paths);
        }

        static void ValidateTile(Tiff tiff, string path)
        {
            int width = tiff.GetField(TiffTag.IMAGEWIDTH)?[0].ToInt() ?? 0;
            int height = tiff.GetField(TiffTag.IMAGELENGTH)?[0].ToInt() ?? 0;
            int bitsPerSample = tiff.GetField(TiffTag.BITSPERSAMPLE)?[0].ToInt() ?? 0;
            SampleFormat sampleFormat = (SampleFormat)(tiff.GetField(TiffTag.SAMPLEFORMAT)?[0].ToInt() ?? 0);

            if (width != TileSize || height != TileSize)
            {
                throw new InvalidDataException(
                    path + ": expected " + TileSize + "x" + TileSize + ", got " + width + "x" + height + "."
                );
            }

            if (bitsPerSample != 16)
            {
                throw new InvalidDataException(path + ": expected 16-bit samples, got " + bitsPerSample + ".");
            }

            if (sampleFormat != SampleFormat.INT)
            {
                throw new InvalidDataException(
                    path + ": expected signed integer samples (SampleFormat.INT), got " + sampleFormat + "."
                );
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < Readers.Length; i++)
            {
                Readers[i].Dispose();
            }
        }
    }
}
