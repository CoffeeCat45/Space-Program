using System;
using System.IO;

/// <summary>
/// Box-filter downsample for Int16 row-major elevation grids (same geographic layout at all resolutions).
/// </summary>
public static class PlanetElevationRawDownsampler
{
    public static void DownsampleInt16RawFile(
        string sourceRawPath,
        string outputRawPath,
        int targetWidth,
        int targetHeight,
        int sourceWidth = PlanetElevationRawFormat.Width,
        int sourceHeight = PlanetElevationRawFormat.Height
    )
    {
        if (string.IsNullOrWhiteSpace(sourceRawPath))
        {
            throw new ArgumentException("Source path is required.", nameof(sourceRawPath));
        }

        if (string.IsNullOrWhiteSpace(outputRawPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputRawPath));
        }

        if (!File.Exists(sourceRawPath))
        {
            throw new FileNotFoundException("Source elevation raw file was not found.", sourceRawPath);
        }

        long expectedSourceBytes = (long)sourceWidth * sourceHeight * PlanetElevationRawFormat.BytesPerSample;
        long sourceLength = new FileInfo(sourceRawPath).Length;

        if (sourceLength < expectedSourceBytes)
        {
            throw new InvalidDataException(
                "Source raw file is too small. Expected at least " + expectedSourceBytes +
                " bytes, got " + sourceLength + "."
            );
        }

        string outputDirectory = Path.GetDirectoryName(outputRawPath);

        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using FileStream sourceStream = File.OpenRead(sourceRawPath);
        using FileStream outputStream = File.Create(outputRawPath);
        using BinaryReader reader = new BinaryReader(sourceStream);
        using BinaryWriter writer = new BinaryWriter(outputStream);

        var sourceRow = new short[sourceWidth];
        var targetRow = new short[targetWidth];

        for (int targetY = 0; targetY < targetHeight; targetY++)
        {
            double sourceY0 = targetY * (sourceHeight / (double)targetHeight);
            double sourceY1 = (targetY + 1) * (sourceHeight / (double)targetHeight);
            int yStart = Math.Max(0, (int)Math.Floor(sourceY0));
            int yEnd = Math.Min(sourceHeight, (int)Math.Ceiling(sourceY1));

            var sums = new long[targetWidth];
            var counts = new int[targetWidth];

            for (int sourceY = yStart; sourceY < yEnd; sourceY++)
            {
                ReadSourceRow(reader, sourceStream, sourceWidth, sourceY, sourceRow);

                for (int targetX = 0; targetX < targetWidth; targetX++)
                {
                    double sourceX0 = targetX * (sourceWidth / (double)targetWidth);
                    double sourceX1 = (targetX + 1) * (sourceWidth / (double)targetWidth);
                    int xStart = Math.Max(0, (int)Math.Floor(sourceX0));
                    int xEnd = Math.Min(sourceWidth, (int)Math.Ceiling(sourceX1));

                    long sum = 0;
                    int count = 0;

                    for (int sourceX = xStart; sourceX < xEnd; sourceX++)
                    {
                        sum += sourceRow[sourceX];
                        count++;
                    }

                    sums[targetX] += sum;
                    counts[targetX] += count;
                }
            }

            for (int targetX = 0; targetX < targetWidth; targetX++)
            {
                targetRow[targetX] = counts[targetX] > 0 ?
                    (short)Math.Round(sums[targetX] / (double)counts[targetX]) :
                    (short)0;
            }

            for (int targetX = 0; targetX < targetWidth; targetX++)
            {
                writer.Write(targetRow[targetX]);
            }
        }
    }

    static void ReadSourceRow(
        BinaryReader reader,
        FileStream sourceStream,
        int sourceWidth,
        int sourceY,
        short[] destination
    )
    {
        long rowOffset = (long)sourceY * sourceWidth * PlanetElevationRawFormat.BytesPerSample;
        sourceStream.Seek(rowOffset, SeekOrigin.Begin);

        for (int x = 0; x < sourceWidth; x++)
        {
            destination[x] = reader.ReadInt16();
        }
    }
}
