using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Runtime DDS loader for RSS / KSP planet textures (DXT1, DXT5, uncompressed RGBA).
/// </summary>
static class DdsTextureLoader
{
    const int DdsHeaderSize = 128;

    public static bool CanLoad(string path)
    {
        return !string.IsNullOrEmpty(path) &&
            path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase);
    }

    public static Texture2D LoadColorMap(string path)
    {
        return Load(path, linear: false, forceDecompress: false, flipRowsForWorldMap: false);
    }

    public static Texture2D LoadNormalMap(string path)
    {
        return Load(path, linear: true, forceDecompress: false, flipRowsForWorldMap: false);
    }

    public static Texture2D LoadDataMap(string path)
    {
        return Load(path, linear: true, forceDecompress: false, flipRowsForWorldMap: false);
    }

    /// <summary>
    /// RSS-Terrain world equirectangular maps (EarthHeight, EarthColor, Earth_NRM) store
    /// row 0 as the south pole. Flip rows at load so v=0 is the north pole, then bake the
    /// KSP/RSS longitude remap (horizontal flip + ¼-width west shift) into texture columns
    /// so every consumer samples with standard geographic U. DXT color/normal maps decompress
    /// to RGBA first, then flip whole pixel rows (same convention as R16 height). Do not use
    /// FlipCompressedBlockRows before decompress — it misaligns color columns relative to
    /// EarthHeight at the same geographic UV.
    /// </summary>
    public static Texture2D LoadWorldColorMap(string path)
    {
        return Load(path, linear: false, forceDecompress: true, flipRowsForWorldMap: true);
    }

    public static Texture2D LoadWorldNormalMap(string path)
    {
        return Load(path, linear: true, forceDecompress: true, flipRowsForWorldMap: true);
    }

    static Texture2D Load(string path, bool linear, bool forceDecompress, bool flipRowsForWorldMap)
    {
        byte[] bytes = File.ReadAllBytes(path);

        if (bytes.Length < DdsHeaderSize)
        {
            throw new InvalidDataException(
                "DDS file is too small: " + path
            );
        }

        if (bytes[0] != (byte)'D' ||
            bytes[1] != (byte)'D' ||
            bytes[2] != (byte)'S' ||
            bytes[3] != (byte)' ')
        {
            throw new InvalidDataException(
                "Not a DDS file: " + path
            );
        }

        int height = BitConverter.ToInt32(bytes, 12);
        int width = BitConverter.ToInt32(bytes, 16);
        int mipMapCount = Math.Max(1, BitConverter.ToInt32(bytes, 28));
        uint pixelFormatFlags = BitConverter.ToUInt32(bytes, 80);
        uint fourCC = BitConverter.ToUInt32(bytes, 84);
        uint rgbBitCount = BitConverter.ToUInt32(bytes, 88);

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException(
                "Invalid DDS dimensions in " + path
            );
        }

        TextureFormat format;
        int dataOffset = DdsHeaderSize;

        if ((pixelFormatFlags & 0x4) != 0)
        {
            format = ResolveCompressedFormat(fourCC);
        }
        else if (rgbBitCount == 32)
        {
            format = TextureFormat.BGRA32;
        }
        else if (rgbBitCount == 24)
        {
            throw new NotSupportedException(
                "24-bit uncompressed DDS is not supported: " + path
            );
        }
        else
        {
            throw new NotSupportedException(
                "Unsupported DDS pixel format in " + path
            );
        }

        if (!SystemInfo.SupportsTextureFormat(format))
        {
            throw new NotSupportedException(
                "Graphics device does not support " + format + " for " + path
            );
        }

        // Flipping only the base mip (block-row order) and letting Unity regenerate the
        // rest is far simpler than flipping every level of a pre-baked mip chain.
        bool useMipChain = mipMapCount > 1 && !flipRowsForWorldMap;

        try
        {
            return CreateTextureFromCompressedPayload(
                path,
                bytes,
                dataOffset,
                width,
                height,
                format,
                useMipChain,
                linear,
                forceDecompress,
                flipRowsForWorldMap
            );
        }
        catch (Exception exception) when (useMipChain)
        {
            Debug.LogWarning(
                "DdsTextureLoader: full mip chain load failed for " +
                path +
                " (" +
                exception.Message +
                "). Retrying base mip only."
            );

            return CreateTextureFromCompressedPayload(
                path,
                bytes,
                dataOffset,
                width,
                height,
                format,
                useMipChain: false,
                linear,
                forceDecompress,
                flipRowsForWorldMap
            );
        }
    }

    static Texture2D CreateTextureFromCompressedPayload(
        string path,
        byte[] bytes,
        int dataOffset,
        int width,
        int height,
        TextureFormat format,
        bool useMipChain,
        bool linear,
        bool forceDecompress,
        bool flipRowsForWorldMap
    )
    {
        int payloadLength = useMipChain ?
            bytes.Length - dataOffset :
            GetCompressedMipSize(width, height, format);

        if (dataOffset + payloadLength > bytes.Length)
        {
            throw new InvalidDataException(
                "DDS payload is truncated for " + path
            );
        }

        byte[] payload = CopyPayload(bytes, dataOffset, payloadLength);

        // DXT world maps that stay compressed: flip whole 4x4 block rows before upload.
        // DXT world maps that will be decompressed: skip block-row flips here and flip
        // whole pixel rows after RGBA decode so columns match the R16 height path.
        if (flipRowsForWorldMap && !useMipChain && !forceDecompress)
        {
            payload = FlipCompressedBlockRows(payload, width, height, format);
        }

        // LoadRawTextureData validates the byte array against the texture's OWN expected
        // total size, which includes every mip level once the texture is constructed with
        // mipChain: true — it does NOT mean "generate mips later from just the base level
        // I happen to Apply now". Passing only the flipped base-mip payload to a
        // mipChain:true texture throws "not enough data provided (overread)". Keep this
        // tied strictly to useMipChain (do we actually have full mip data in `payload`),
        // never to flipRowsForWorldMap. The flipped textures simply don't get mips —
        // fine for a slowly-varying macro/normal map, and far better than failing to load.
        Texture2D texture = new Texture2D(
            width,
            height,
            format,
            useMipChain,
            linear
        );
        texture.LoadRawTextureData(payload);
        texture.Apply(updateMipmaps: useMipChain, makeNoLongerReadable: false);
        texture.name = Path.GetFileNameWithoutExtension(path);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;

        if (!ShouldDecompressForGpuSampling(bytes.Length - dataOffset, forceDecompress))
        {
            return texture;
        }

        // World-mapped textures were loaded above with mipChain:false (see the comment on
        // useMipChain), so without this they have exactly one texel resolution to sample
        // from no matter how far away or how obliquely the camera views the planet — the
        // GPU can't blend toward a coarser mip, so distant/glancing views alias into a
        // jagged, "sawtooth" coastline instead of a smooth blend. Decompressing here also
        // downsamples (this map is only ever used for a soft macro/orbital blend, never
        // close-up detail) and builds a real mip chain from the smaller result, which is
        // both correct and dramatically cheaper on RAM than mip-ing the full 8192x4096.
        int maxDimensionAfterDecompress = flipRowsForWorldMap ? 2048 : 0;
        Texture2D decompressed = DecompressForGpuSampling(
            texture,
            linear,
            maxDimensionAfterDecompress,
            flipRowsAfterDecompress: flipRowsForWorldMap
        );

        if (flipRowsForWorldMap && decompressed != null)
        {
            RemapColumnsToGeographicLayout(decompressed);
        }

        return decompressed;
    }

    /// <summary>
    /// Reverses the vertical order of whole 4x4 compressed blocks without touching each
    /// block's own internal 4 texel rows. That leaves at most a ~4-pixel vertical seam
    /// artifact — invisible on an 8192x4096 planet-scale map — while avoiding a full
    /// decompress/flip/recompress round trip (Unity has no runtime DXT encoder) or having
    /// to bit-twiddle each block's packed per-row indices by hand.
    /// </summary>
    static byte[] FlipCompressedBlockRows(byte[] payload, int width, int height, TextureFormat format)
    {
        int blockBytes = format == TextureFormat.DXT1 ? 8 : 16;
        int blocksWide = Math.Max(1, (width + 3) / 4);
        int blocksHigh = Math.Max(1, (height + 3) / 4);
        int rowBytes = blocksWide * blockBytes;

        if (rowBytes * blocksHigh > payload.Length)
        {
            return payload;
        }

        byte[] flipped = new byte[payload.Length];

        for (int blockRow = 0; blockRow < blocksHigh; blockRow++)
        {
            int sourceOffset = (blocksHigh - 1 - blockRow) * rowBytes;
            int targetOffset = blockRow * rowBytes;
            Buffer.BlockCopy(payload, sourceOffset, flipped, targetOffset, rowBytes);
        }

        return flipped;
    }

    static bool ShouldDecompressForGpuSampling(int compressedPayloadBytes, bool forceDecompress)
    {
        if (forceDecompress)
        {
            return true;
        }

        // Keep large data maps compressed when possible to save RAM.
        const int maxCompressedBytesBeforeDecompress = 8 * 1024 * 1024;
        return compressedPayloadBytes <= maxCompressedBytesBeforeDecompress;
    }

    /// <summary>
    /// Small runtime DDS payloads may be blitted to RGBA32 when compressed sampling fails.
    /// When <paramref name="maxDimension"/> is &gt; 0, also downsamples and builds a real
    /// mip chain (via a second GPU blit into a smaller RenderTexture) — needed for the
    /// mipChain:false textures produced by the world-map flip path, or distant/oblique
    /// views alias badly with no coarser mip to fall back to.
    /// </summary>
    static Texture2D DecompressForGpuSampling(
        Texture2D texture,
        bool linear,
        int maxDimension = 0,
        bool flipRowsAfterDecompress = false
    )
    {
        if (texture == null)
        {
            return null;
        }

        if (!texture.format.ToString().StartsWith("DXT", StringComparison.Ordinal))
        {
            return texture;
        }

        int sourceWidth = texture.width;
        int sourceHeight = texture.height;
        int targetWidth = sourceWidth;
        int targetHeight = sourceHeight;

        if (maxDimension > 0 && (targetWidth > maxDimension || targetHeight > maxDimension))
        {
            ComputeDownsampleDimensions(
                sourceWidth,
                sourceHeight,
                maxDimension,
                out targetWidth,
                out targetHeight
            );
        }

        bool buildMips = maxDimension > 0;
        RenderTextureReadWrite readWrite = linear ?
            RenderTextureReadWrite.Linear :
            RenderTextureReadWrite.sRGB;
        RenderTexture fullResTarget = RenderTexture.GetTemporary(
            sourceWidth,
            sourceHeight,
            0,
            RenderTextureFormat.ARGB32,
            readWrite
        );

        try
        {
            Graphics.Blit(texture, fullResTarget);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = fullResTarget;

            Texture2D decompressed = new Texture2D(
                sourceWidth,
                sourceHeight,
                TextureFormat.RGBA32,
                mipChain: false,
                linear
            );
            decompressed.ReadPixels(new Rect(0, 0, sourceWidth, sourceHeight), 0, 0);
            decompressed.Apply(updateMipmaps: false);
            decompressed.name = texture.name;
            decompressed.wrapMode = texture.wrapMode;
            decompressed.filterMode = texture.filterMode;

            RenderTexture.active = previous;
            UnityEngine.Object.Destroy(texture);

            if (flipRowsAfterDecompress)
            {
                FlipTextureRowsVertically(decompressed, updateMipmaps: false);
            }

            if (targetWidth != sourceWidth || targetHeight != sourceHeight)
            {
                Texture2D downsampled = DownsampleTexture(
                    decompressed,
                    targetWidth,
                    targetHeight,
                    linear,
                    buildMips
                );
                UnityEngine.Object.Destroy(decompressed);
                return downsampled;
            }

            if (buildMips)
            {
                decompressed.Apply(updateMipmaps: true);
            }

            return decompressed;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "DdsTextureLoader: failed to decompress " +
                texture.name +
                " (" +
                exception.Message +
                "); using compressed payload."
            );
            return texture;
        }
        finally
        {
            RenderTexture.ReleaseTemporary(fullResTarget);
        }
    }

    static Texture2D DownsampleTexture(
        Texture2D source,
        int targetWidth,
        int targetHeight,
        bool linear,
        bool buildMips
    )
    {
        Color[] srcPixels = source.GetPixels();
        int sourceWidth = source.width;
        int sourceHeight = source.height;
        Color[] targetPixels = new Color[targetWidth * targetHeight];

        for (int y = 0; y < targetHeight; y++)
        {
            int sourceY = (y * sourceHeight) / targetHeight;

            for (int x = 0; x < targetWidth; x++)
            {
                int sourceX = (x * sourceWidth) / targetWidth;
                targetPixels[(y * targetWidth) + x] =
                    srcPixels[(sourceY * sourceWidth) + sourceX];
            }
        }

        Texture2D downsampled = new Texture2D(
            targetWidth,
            targetHeight,
            TextureFormat.RGBA32,
            buildMips,
            linear
        );
        downsampled.SetPixels(targetPixels);
        downsampled.Apply(updateMipmaps: buildMips);
        downsampled.name = source.name;
        downsampled.wrapMode = source.wrapMode;
        downsampled.filterMode = source.filterMode;
        return downsampled;
    }

    static int GetCompressedMipSize(int width, int height, TextureFormat format)
    {
        int blockBytes = format == TextureFormat.DXT1 ? 8 : 16;
        int blockWidth = Math.Max(1, (width + 3) / 4);
        int blockHeight = Math.Max(1, (height + 3) / 4);
        return blockWidth * blockHeight * blockBytes;
    }

    static TextureFormat ResolveCompressedFormat(uint fourCC)
    {
        if (fourCC == 0x31545844)
        {
            return TextureFormat.DXT1;
        }

        if (fourCC == 0x35545844)
        {
            return TextureFormat.DXT5;
        }

        throw new NotSupportedException(
            "Unsupported DDS FourCC: 0x" + fourCC.ToString("X8")
        );
    }

    public static Texture2D LoadUInt16GrayscaleHeightMap(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        if (bytes.Length < DdsHeaderSize)
        {
            throw new InvalidDataException("DDS file is too small: " + path);
        }

        int height = BitConverter.ToInt32(bytes, 12);
        int width = BitConverter.ToInt32(bytes, 16);
        uint fourCC = BitConverter.ToUInt32(bytes, 84);
        uint rgbBitCount = BitConverter.ToUInt32(bytes, 88);

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("Invalid DDS dimensions in " + path);
        }

        if (fourCC != 0 || rgbBitCount != 16)
        {
            throw new NotSupportedException(
                "RSS height DDS must be uncompressed 16-bit grayscale: " + path
            );
        }

        const int MaxHeightDimension = 8192;

        if (width > MaxHeightDimension || height > MaxHeightDimension)
        {
            return LoadDownsampledUInt16HeightMap(bytes, width, height, path, MaxHeightDimension);
        }

        int pixelBytes = width * height * 2;
        int dataOffset = DdsHeaderSize;

        if (bytes.Length < dataOffset + pixelBytes)
        {
            throw new InvalidDataException(
                "DDS height payload is truncated: " + path
            );
        }

        Texture2D texture = new Texture2D(
            width,
            height,
            TextureFormat.R16,
            false,
            true
        );
        texture.LoadRawTextureData(
            FlipRowsVertically(bytes, dataOffset, width, height)
        );
        texture.Apply(false, false);
        texture.name = Path.GetFileNameWithoutExtension(path);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        RemapColumnsToGeographicLayout(texture);
        return texture;
    }

    /// <summary>
    /// RSS height/displacement DDS files store row 0 as the south pole — the opposite
    /// vertical convention from the color/albedo/splat maps (row 0 = north pole there).
    /// Sampling them both with the same v=0-is-north UV silently read every height
    /// sample from the wrong (mirrored) latitude, so real mountains/coastlines never
    /// lined up with where the vertex shader displaced them — the ground just looked
    /// flat and featureless everywhere. Flip rows once here so every consumer of this
    /// texture shares the same north-at-row-0 convention as everything else.
    /// </summary>
    static byte[] FlipRowsVertically(byte[] source, int dataOffset, int width, int height)
    {
        int rowBytes = width * 2;
        byte[] flipped = new byte[rowBytes * height];

        for (int y = 0; y < height; y++)
        {
            int sourceRowOffset = dataOffset + ((height - 1 - y) * rowBytes);
            int targetRowOffset = y * rowBytes;
            Buffer.BlockCopy(source, sourceRowOffset, flipped, targetRowOffset, rowBytes);
        }

        return flipped;
    }

    static Texture2D LoadDownsampledUInt16HeightMap(
        byte[] bytes,
        int sourceWidth,
        int sourceHeight,
        string path,
        int maxDimension
    )
    {
        ComputeDownsampleDimensions(
            sourceWidth,
            sourceHeight,
            maxDimension,
            out int targetWidth,
            out int targetHeight
        );
        int dataOffset = DdsHeaderSize;
        ushort[] source = new ushort[sourceWidth * sourceHeight];

        // Flip vertically while reading: row 0 of the file is the south pole, but every
        // consumer downstream expects v=0 (row 0) to be the north pole. See
        // FlipRowsVertically for the full explanation.
        for (int y = 0; y < sourceHeight; y++)
        {
            int flippedSourceRow = sourceHeight - 1 - y;
            int rowOffset = dataOffset + (flippedSourceRow * sourceWidth * 2);

            for (int x = 0; x < sourceWidth; x++)
            {
                source[(y * sourceWidth) + x] = BitConverter.ToUInt16(
                    bytes,
                    rowOffset + (x * 2)
                );
            }
        }

        ushort[] target = new ushort[targetWidth * targetHeight];

        for (int y = 0; y < targetHeight; y++)
        {
            int sourceY = (y * sourceHeight) / targetHeight;

            for (int x = 0; x < targetWidth; x++)
            {
                int sourceX = (x * sourceWidth) / targetWidth;
                target[(y * targetWidth) + x] = source[(sourceY * sourceWidth) + sourceX];
            }
        }

        Texture2D texture = new Texture2D(
            targetWidth,
            targetHeight,
            TextureFormat.R16,
            false,
            true
        );
        byte[] targetBytes = new byte[target.Length * sizeof(ushort)];
        Buffer.BlockCopy(target, 0, targetBytes, 0, targetBytes.Length);
        texture.LoadRawTextureData(targetBytes);
        texture.Apply(false, false);
        texture.name = Path.GetFileNameWithoutExtension(path) + "_Downsampled";
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        RemapColumnsToGeographicLayout(texture);
        return texture;
    }

    /// <summary>
    /// Rewrites each column so geographicU indexes the texel that RSS/KSP stored at rssU.
    /// After this, sample legacy RSS maps with geographic U and flipped V only.
    /// </summary>
    static void RemapColumnsToGeographicLayout(Texture2D texture)
    {
        if (texture == null || texture.width < 2)
        {
            return;
        }

        int width = texture.width;
        int height = texture.height;

        if (texture.format == TextureFormat.R16)
        {
            byte[] raw = texture.GetRawTextureData();
            int pixelCount = width * height;
            ushort[] heightPixels = new ushort[pixelCount];
            Buffer.BlockCopy(raw, 0, heightPixels, 0, raw.Length);
            ushort[] remappedHeight = RemapColumnValues(heightPixels, width, height);
            byte[] targetBytes = new byte[remappedHeight.Length * sizeof(ushort)];
            Buffer.BlockCopy(remappedHeight, 0, targetBytes, 0, targetBytes.Length);
            texture.LoadRawTextureData(targetBytes);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return;
        }

        Color[] colorPixels = texture.GetPixels();
        Color[] remappedColors = RemapColumnColors(colorPixels, width, height);
        texture.SetPixels(remappedColors);
        texture.Apply(
            updateMipmaps: texture.mipmapCount > 1,
            makeNoLongerReadable: false
        );
    }

    static int SourceColumnForGeographicColumn(int geographicColumn, int width)
    {
        float geographicU = geographicColumn / (float)(width - 1);
        double rssU = PlanetGeographicCoords.MapGeographicUToRssEarthTextureU(geographicU);
        return Mathf.Clamp(
            Mathf.RoundToInt((float)rssU * (width - 1)),
            0,
            width - 1
        );
    }

    static Color[] RemapColumnColors(Color[] source, int width, int height)
    {
        Color[] output = new Color[source.Length];

        for (int y = 0; y < height; y++)
        {
            int row = y * width;

            for (int x = 0; x < width; x++)
            {
                int sourceX = SourceColumnForGeographicColumn(x, width);
                output[row + x] = source[row + sourceX];
            }
        }

        return output;
    }

    static ushort[] RemapColumnValues(ushort[] source, int width, int height)
    {
        ushort[] output = new ushort[source.Length];

        for (int y = 0; y < height; y++)
        {
            int row = y * width;

            for (int x = 0; x < width; x++)
            {
                int sourceX = SourceColumnForGeographicColumn(x, width);
                output[row + x] = source[row + sourceX];
            }
        }

        return output;
    }

    static void ComputeDownsampleDimensions(
        int sourceWidth,
        int sourceHeight,
        int maxDimension,
        out int targetWidth,
        out int targetHeight
    )
    {
        if (sourceWidth <= maxDimension && sourceHeight <= maxDimension)
        {
            targetWidth = sourceWidth;
            targetHeight = sourceHeight;
            return;
        }

        float aspect = (float)sourceWidth / sourceHeight;

        if (sourceWidth >= sourceHeight)
        {
            targetWidth = maxDimension;
            targetHeight = Mathf.Max(1, Mathf.RoundToInt(maxDimension / aspect));
            return;
        }

        targetHeight = maxDimension;
        targetWidth = Mathf.Max(1, Mathf.RoundToInt(maxDimension * aspect));
    }


    static void FlipTextureRowsVertically(Texture2D texture, bool updateMipmaps)
    {
        int width = texture.width;
        int height = texture.height;
        Color32[] pixels = texture.GetPixels32();
        Color32[] flipped = new Color32[pixels.Length];
        int rowStride = width;

        for (int y = 0; y < height; y++)
        {
            int sourceRow = (height - 1 - y) * rowStride;
            int targetRow = y * rowStride;
            Array.Copy(pixels, sourceRow, flipped, targetRow, rowStride);
        }

        texture.SetPixels32(flipped);
        texture.Apply(updateMipmaps);
    }

    static byte[] CopyPayload(byte[] source, int offset, int length)
    {
        byte[] payload = new byte[length];
        Buffer.BlockCopy(source, offset, payload, 0, length);
        return payload;
    }
}
