using System;
using UnityEngine;

/// <summary>
/// Thread-safe CPU mesh generation for sandbox (vertex paint) and flight (biome + flatten).
/// Coordinate canon: work_terrain.md (sim UV + Mesh Frame X-flip).
/// </summary>
public static class PlanetFlightChunkCpuBuilder
{
    static readonly Color LandColor = Color.white;
    static readonly Color OceanColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    const float OceanDepthPackMeters = 2500f;

    public readonly struct FlightBiomeBuildParams
    {
        public readonly float FlattenRadiusMeters;
        public readonly float HardFlattenRadiusMeters;
        public readonly float TargetPadHeightMeters;
        public readonly float LaunchSiteDirX;
        public readonly float LaunchSiteDirY;
        public readonly float LaunchSiteDirZ;
        public readonly float MaxHeightDeltaMeters;

        public FlightBiomeBuildParams(
            float flattenRadiusMeters,
            float hardFlattenRadiusMeters,
            float targetPadHeightMeters,
            Vector3 launchSiteDirectionUnity,
            float maxHeightDeltaMeters
        )
        {
            FlattenRadiusMeters = flattenRadiusMeters;
            HardFlattenRadiusMeters = hardFlattenRadiusMeters;
            TargetPadHeightMeters = targetPadHeightMeters;
            LaunchSiteDirX = launchSiteDirectionUnity.x;
            LaunchSiteDirY = launchSiteDirectionUnity.y;
            LaunchSiteDirZ = launchSiteDirectionUnity.z;
            MaxHeightDeltaMeters = maxHeightDeltaMeters;
        }
    }

    public static PlanetFlightChunkMeshPipeline.ChunkMeshCpuData Build(
        PlanetFlightChunkMeshPipeline.ChunkMeshBuildRequest request,
        PlanetElevationMmapField elevationFieldFallback,
        PlanetFlightChunkMeshCache.BuildStyle style,
        FlightBiomeBuildParams? flightParams = null
    )
    {
        int resolution = ClampResolution(request.Resolution);
        int vertexCount = resolution * resolution;
        var vertices = new Vector3[vertexCount];
        var geographicUvs = new Vector2[vertexCount];
        bool writeFlightWaterMask =
            style == PlanetFlightChunkMeshCache.BuildStyle.FlightBiome &&
            CelestialTerrainRuntime.HasOcean;
        bool useLandOceanVertexPaint =
            style == PlanetFlightChunkMeshCache.BuildStyle.SandboxLandOcean ||
            request.UseLandOceanVertexPaint;
        Color[] vertexColors = useLandOceanVertexPaint || writeFlightWaterMask ?
            new Color[vertexCount] :
            null;
        Vector2[] detailUvs = style == PlanetFlightChunkMeshCache.BuildStyle.FlightBiome ?
            new Vector2[vertexCount] :
            null;
        var triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        int triangleIndex = 0;
        double invResolutionMinusOne = resolution > 1 ? 1.0 / (resolution - 1) : 0.0;

        bool useHeightBuffer = style == PlanetFlightChunkMeshCache.BuildStyle.FlightBiome &&
            flightParams.HasValue &&
            flightParams.Value.MaxHeightDeltaMeters > 0f;
        float[] heights = useHeightBuffer ? new float[vertexCount] : null;
        double[] rawGebcoMeters = writeFlightWaterMask ?
            new double[vertexCount] :
            null;

        for (int y = 0; y < resolution; y++)
        {
            double gridV = resolution > 1 ? y * invResolutionMinusOne : 0.0;
            float faceV = (float)(request.MinV + ((request.MaxV - request.MinV) * gridV));

            for (int x = 0; x < resolution; x++)
            {
                double gridU = resolution > 1 ? x * invResolutionMinusOne : 0.0;
                float faceU = (float)(request.MinU + ((request.MaxU - request.MinU) * gridU));
                float sampleU = faceU;
                float sampleV = faceV;
                InsetPolarFaceUv(request.FaceIndex, ref sampleU, ref sampleV);
                int vertexIndex = x + (y * resolution);

                SimVector3 simUnit = PlanetCubeSphereCoords.FaceUvToUnitDirection(
                    request.FaceIndex,
                    sampleU,
                    sampleV
                );
                PlanetGeographicCoords.DirectionToGeographicUv(
                    simUnit,
                    out double geographicU,
                    out double geographicV
                );
                geographicUvs[vertexIndex] = new Vector2((float)geographicU, (float)geographicV);

                if (heights != null || rawGebcoMeters != null)
                {
                    double elevationMeters = SampleElevationMeters(
                        simUnit,
                        request.FaceIndex,
                        sampleU,
                        sampleV,
                        request.Lod,
                        elevationFieldFallback
                    );

                    if (rawGebcoMeters != null)
                    {
                        rawGebcoMeters[vertexIndex] = elevationMeters;
                    }

                    if (heights != null)
                    {
                        heights[vertexIndex] = SanitizeHeightOffsetMeters(
                            SampleFlightHeightOffsetMeters(
                                elevationMeters,
                                simUnit,
                                flightParams.Value
                            )
                        );
                    }
                }

                if (detailUvs != null)
                {
                    detailUvs[vertexIndex] = PlanetFlightDetailUv.ComputeWrappedDetailMetersUv(
                        simUnit,
                        request.PlanetRadiusMeters
                    );
                }
            }
        }

        if (heights != null && flightParams.HasValue && flightParams.Value.MaxHeightDeltaMeters > 0f)
        {
            ClampMeshHeightSlopes(heights, resolution, flightParams.Value.MaxHeightDeltaMeters);
        }

        for (int y = 0; y < resolution; y++)
        {
            double gridV = resolution > 1 ? y * invResolutionMinusOne : 0.0;
            float faceV = (float)(request.MinV + ((request.MaxV - request.MinV) * gridV));

            for (int x = 0; x < resolution; x++)
            {
                double gridU = resolution > 1 ? x * invResolutionMinusOne : 0.0;
                float faceU = (float)(request.MinU + ((request.MaxU - request.MinU) * gridU));
                float sampleU = faceU;
                float sampleV = faceV;
                InsetPolarFaceUv(request.FaceIndex, ref sampleU, ref sampleV);
                int vertexIndex = x + (y * resolution);

                SimVector3 simUnit = PlanetCubeSphereCoords.FaceUvToUnitDirection(
                    request.FaceIndex,
                    sampleU,
                    sampleV
                );

                if (style == PlanetFlightChunkMeshCache.BuildStyle.SandboxLandOcean)
                {
                    WriteSandboxVertex(
                        vertices,
                        vertexColors,
                        vertexIndex,
                        simUnit,
                        request.FaceIndex,
                        sampleU,
                        sampleV,
                        request.PlanetRadiusMeters,
                        request.Lod,
                        elevationFieldFallback
                    );
                }
                else
                {
                    WriteFlightBiomeVertex(
                        vertices,
                        vertexIndex,
                        simUnit,
                        request.PlanetRadiusMeters,
                        heights,
                        flightParams
                    );

                    if (vertexColors != null && rawGebcoMeters != null)
                    {
                        vertexColors[vertexIndex] = PackFlightSurfaceVertexColor(
                            rawGebcoMeters[vertexIndex]
                        );
                    }
                }

                if (x >= resolution - 1 || y >= resolution - 1)
                {
                    continue;
                }

                int i = vertexIndex;
                triangles[triangleIndex++] = i;
                triangles[triangleIndex++] = i + resolution + 1;
                triangles[triangleIndex++] = i + resolution;
                triangles[triangleIndex++] = i;
                triangles[triangleIndex++] = i + 1;
                triangles[triangleIndex++] = i + resolution + 1;
            }
        }

        float chunkSpanMeters = request.PlanetRadiusMeters * 2f /
            Mathf.Max(1, 1 << Mathf.Clamp(request.Lod, 0, 30));
        float skirtDepthMeters = (float)PlanetPqsSkirtBuilder.ComputeSkirtDepthMeters(
            chunkSpanMeters,
            PlanetPqsConstants.DefaultSkirtDepthFraction,
            resolution,
            request.Lod
        );
        if (skirtDepthMeters > 0f)
        {
            AppendRadialSkirts(
                ref vertices,
                ref triangles,
                ref geographicUvs,
                ref detailUvs,
                ref vertexColors,
                resolution,
                skirtDepthMeters
            );
        }

        return new PlanetFlightChunkMeshPipeline.ChunkMeshCpuData
        {
            Vertices = vertices,
            Triangles = triangles,
            Colors = vertexColors,
            GeographicUvs = geographicUvs,
            DetailUvs = detailUvs,
        };
    }

    static Color PackFlightSurfaceVertexColor(double elevationMeters)
    {
        if (elevationMeters > 0.0)
        {
            return new Color(1f, 0f, 1f, 0f);
        }

        float packedDepth = Mathf.Clamp01((float)(-elevationMeters / OceanDepthPackMeters));
        return new Color(1f, packedDepth, 1f, 1f);
    }

    static void WriteSandboxVertex(
        Vector3[] vertices,
        Color[] vertexColors,
        int vertexIndex,
        SimVector3 simUnit,
        int faceIndex,
        float faceU,
        float faceV,
        float planetRadiusMeters,
        int lod,
        PlanetElevationMmapField elevationFieldFallback
    )
    {
        double elevationMeters = SampleElevationMeters(
            simUnit,
            faceIndex,
            faceU,
            faceV,
            lod,
            elevationFieldFallback
        );
        float visualOffsetMeters = (float)(
            elevationMeters * PlanetSpherifiedChunkMeshBuilder.ActiveVisualHeightScale
        );
        Vector3 meshUnit = new Vector3(
            -(float)simUnit.x,
            (float)simUnit.y,
            (float)simUnit.z
        );
        vertices[vertexIndex] = SanitizeSurfaceVertex(
            meshUnit * (planetRadiusMeters + visualOffsetMeters),
            meshUnit,
            planetRadiusMeters
        );
        vertexColors[vertexIndex] =
            CelestialTerrainRuntime.HasOcean && elevationMeters <= 0.0 ?
                OceanColor :
                LandColor;
    }

    static void WriteFlightBiomeVertex(
        Vector3[] vertices,
        int vertexIndex,
        SimVector3 simUnit,
        float planetRadiusMeters,
        float[] heights,
        FlightBiomeBuildParams? flightParams
    )
    {
        Vector3 unitDirection = new Vector3(
            (float)simUnit.x,
            (float)simUnit.y,
            (float)simUnit.z
        );
        float heightOffset = heights != null ?
            heights[vertexIndex] :
            0f;
        Vector3 surfacePoint = unitDirection * (planetRadiusMeters + heightOffset);

        if (flightParams.HasValue)
        {
            surfacePoint = ApplyLaunchSiteFlattenSurface(
                surfacePoint,
                unitDirection,
                planetRadiusMeters,
                flightParams.Value
            );
        }

        vertices[vertexIndex] = SanitizeSurfaceVertex(
            PlanetCubeSphere.FlipMeshVertexPositionX(surfacePoint),
            unitDirection,
            planetRadiusMeters
        );
    }

    static float SanitizeHeightOffsetMeters(float heightOffsetMeters)
    {
        return float.IsNaN(heightOffsetMeters) || float.IsInfinity(heightOffsetMeters) ?
            0f :
            heightOffsetMeters;
    }

    static Vector3 SanitizeSurfaceVertex(
        Vector3 meshVertex,
        Vector3 unitDirection,
        float planetRadiusMeters
    )
    {
        if (IsFiniteVector(meshVertex))
        {
            return meshVertex;
        }

        Vector3 fallbackDirection = unitDirection.sqrMagnitude > 1e-12f ?
            unitDirection.normalized :
            Vector3.up;
        return PlanetCubeSphere.FlipMeshVertexPositionX(fallbackDirection * planetRadiusMeters);
    }

    static bool IsFiniteVector(Vector3 value)
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) &&
            !float.IsInfinity(value.y) &&
            !float.IsInfinity(value.z);
    }

    static float SampleFlightHeightOffsetMeters(
        double elevationMeters,
        SimVector3 simUnit,
        FlightBiomeBuildParams flightParams
    )
    {
        if (CelestialTerrainRuntime.HasOcean && elevationMeters <= 0.0)
        {
            return ApplyLaunchSiteHeightFlatten(0f, simUnit, flightParams);
        }

        float heightOffset = (float)(
            elevationMeters * PlanetSpherifiedChunkMeshBuilder.ActiveVisualHeightScale
        );
        heightOffset = ClampHeightOffsetMeters(heightOffset, simUnit);
        return ApplyLaunchSiteHeightFlatten(heightOffset, simUnit, flightParams);
    }

    static float SampleFlightHeightOffsetMeters(
        SimVector3 simUnit,
        int faceIndex,
        float faceU,
        float faceV,
        int lod,
        PlanetElevationMmapField elevationFieldFallback,
        FlightBiomeBuildParams flightParams
    )
    {
        double elevationMeters = SampleElevationMeters(
            simUnit,
            faceIndex,
            faceU,
            faceV,
            lod,
            elevationFieldFallback
        );
        return SampleFlightHeightOffsetMeters(elevationMeters, simUnit, flightParams);
    }

    static double SampleElevationMeters(
        SimVector3 simUnit,
        int faceIndex,
        float faceU,
        float faceV,
        int lod,
        PlanetElevationMmapField elevationFieldFallback
    )
    {
        if (PlanetElevationCatalog.IsHighResLoaded || PlanetElevationCatalog.IsCoarseLodLoaded)
        {
            return PlanetElevationCatalog.SampleForLod(faceIndex, faceU, faceV, lod);
        }

        if (elevationFieldFallback != null)
        {
            PlanetGeographicCoords.DirectionToGeographicUv(
                simUnit,
                out double geographicU,
                out double geographicV
            );
            PlanetGeographicCoords.GeographicUvToLatitudeLongitude(
                geographicU,
                geographicV,
                out double latitudeRadians,
                out double longitudeRadians
            );
            return PlanetElevationHighResSampler.GetHeightBilinearNew(
                elevationFieldFallback,
                latitudeRadians,
                longitudeRadians
            );
        }

        if (PlanetElevationCatalog.IsLoaded)
        {
            return PlanetElevationCatalog.GetHeightBilinearLegacy(simUnit);
        }

        return 0.0;
    }

    static float ClampHeightOffsetMeters(float heightOffset, SimVector3 simUnit)
    {
        float maxAbsHeightMeters = PlanetElevationCatalog.IsLoaded ?
            12000f * PlanetSpherifiedChunkMeshBuilder.ActiveVisualHeightScale :
            12000f;
        float minHeightMeters = PlanetElevationCatalog.IsLoaded ?
            -11000f * PlanetSpherifiedChunkMeshBuilder.ActiveVisualHeightScale :
            -2500f;
        float clamped = Mathf.Clamp(heightOffset, minHeightMeters, maxAbsHeightMeters);
        float poleBlend = Mathf.InverseLerp(
            0.985f,
            0.9995f,
            Mathf.Abs((float)simUnit.y)
        );

        if (poleBlend <= 0f)
        {
            return clamped;
        }

        return Mathf.Lerp(clamped, 0f, poleBlend);
    }

    static float ApplyLaunchSiteHeightFlatten(
        float heightOffset,
        SimVector3 unitDirectionSim,
        FlightBiomeBuildParams flightParams
    )
    {
        float surfaceDistance = SurfaceDistanceMeters(
            unitDirectionSim,
            flightParams
        );

        if (surfaceDistance <= flightParams.HardFlattenRadiusMeters)
        {
            return flightParams.TargetPadHeightMeters;
        }

        if (surfaceDistance >= flightParams.FlattenRadiusMeters)
        {
            return heightOffset;
        }

        float flattenBlend = Mathf.SmoothStep(
            flightParams.FlattenRadiusMeters,
            flightParams.HardFlattenRadiusMeters,
            surfaceDistance
        );
        return Mathf.Lerp(heightOffset, flightParams.TargetPadHeightMeters, flattenBlend);
    }

    static Vector3 ApplyLaunchSiteFlattenSurface(
        Vector3 surfacePoint,
        Vector3 unitDirection,
        float planetRadiusMeters,
        FlightBiomeBuildParams flightParams
    )
    {
        var launchDir = new SimVector3(
            flightParams.LaunchSiteDirX,
            flightParams.LaunchSiteDirY,
            flightParams.LaunchSiteDirZ
        );
        var unitSim = new SimVector3(unitDirection.x, unitDirection.y, unitDirection.z);
        float surfaceDistance = SurfaceDistanceMeters(unitSim, flightParams);
        float targetRadius = planetRadiusMeters + flightParams.TargetPadHeightMeters;
        Vector3 flatPoint = unitDirection * targetRadius;

        if (surfaceDistance <= flightParams.HardFlattenRadiusMeters)
        {
            return flatPoint;
        }

        if (surfaceDistance >= flightParams.FlattenRadiusMeters)
        {
            return surfacePoint;
        }

        float flattenBlend = Mathf.SmoothStep(
            flightParams.FlattenRadiusMeters,
            flightParams.HardFlattenRadiusMeters,
            surfaceDistance
        );
        return Vector3.Lerp(surfacePoint, flatPoint, flattenBlend);
    }

    static float SurfaceDistanceMeters(SimVector3 unitDirection, FlightBiomeBuildParams flightParams)
    {
        var launchDir = new SimVector3(
            flightParams.LaunchSiteDirX,
            flightParams.LaunchSiteDirY,
            flightParams.LaunchSiteDirZ
        ).Normalize();
        unitDirection = unitDirection.Normalize();
        double dot = SimVector3.Dot(unitDirection, launchDir);
        dot = Math.Clamp(dot, -1.0, 1.0);
        float angleRadians = (float)Math.Acos(dot);
        float planetRadius = CelestialTerrainRuntime.LodBodyRadiusUnity;
        if (planetRadius <= 1f)
        {
            planetRadius = PlanetSurfaceConstants.UnityFlightPlanetRadiusMeters;
        }

        return angleRadians * planetRadius;
    }

    static void ClampMeshHeightSlopes(float[] heights, int resolution, float maxHeightDeltaMeters)
    {
        if (heights == null || resolution < 2 || maxHeightDeltaMeters <= 0f)
        {
            return;
        }

        int last = resolution - 1;
        var raw = new float[heights.Length];
        Array.Copy(heights, raw, heights.Length);

        ClampGridEdge1D(heights, raw, resolution, maxHeightDeltaMeters, row: 0);
        ClampGridEdge1D(heights, raw, resolution, maxHeightDeltaMeters, row: last);
        ClampGridEdge1D(heights, raw, resolution, maxHeightDeltaMeters, column: 0);
        ClampGridEdge1D(heights, raw, resolution, maxHeightDeltaMeters, column: last);
        heights[0] = raw[0];
        heights[last] = raw[last];
        heights[last * resolution] = raw[last * resolution];
        heights[(last * resolution) + last] = raw[(last * resolution) + last];

        const int iterations = 2;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int y = 1; y < last; y++)
            {
                for (int x = 1; x < last; x++)
                {
                    int index = x + (y * resolution);
                    float height = heights[index];
                    height = Mathf.Min(height, heights[index - 1] + maxHeightDeltaMeters);
                    height = Mathf.Max(height, heights[index - 1] - maxHeightDeltaMeters);
                    height = Mathf.Min(height, heights[index - resolution] + maxHeightDeltaMeters);
                    height = Mathf.Max(height, heights[index - resolution] - maxHeightDeltaMeters);
                    heights[index] = height;
                }
            }
        }
    }

    static void ClampGridEdge1D(
        float[] heights,
        float[] raw,
        int resolution,
        float maxHeightDeltaMeters,
        int row = -1,
        int column = -1
    )
    {
        bool alongRow = row >= 0;
        int last = resolution - 1;

        for (int i = 0; i < resolution; i++)
        {
            int index = alongRow ? i + (row * resolution) : column + (i * resolution);
            heights[index] = raw[index];
        }

        const int iterations = 2;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int i = 1; i < resolution; i++)
            {
                int index = alongRow ? i + (row * resolution) : column + (i * resolution);
                int prev = alongRow ? (i - 1) + (row * resolution) : column + ((i - 1) * resolution);
                float height = heights[index];
                height = Mathf.Min(height, heights[prev] + maxHeightDeltaMeters);
                height = Mathf.Max(height, heights[prev] - maxHeightDeltaMeters);
                heights[index] = height;
            }

            for (int i = last - 1; i >= 0; i--)
            {
                int index = alongRow ? i + (row * resolution) : column + (i * resolution);
                int next = alongRow ? (i + 1) + (row * resolution) : column + ((i + 1) * resolution);
                float height = heights[index];
                height = Mathf.Min(height, heights[next] + maxHeightDeltaMeters);
                height = Mathf.Max(height, heights[next] - maxHeightDeltaMeters);
                heights[index] = height;
            }
        }
    }

    static int ClampResolution(int resolution)
    {
        if (resolution < 2)
        {
            return 2;
        }

        if (resolution > 256)
        {
            return 256;
        }

        return resolution;
    }

    static void InsetPolarFaceUv(int faceIndex, ref float u, ref float v, float minRadius = 0.015f)
    {
        if (faceIndex != 0 && faceIndex != 1)
        {
            return;
        }

        float deltaU = u - 0.5f;
        float deltaV = v - 0.5f;
        float radius = (float)Math.Sqrt((deltaU * deltaU) + (deltaV * deltaV));

        if (radius >= minRadius)
        {
            return;
        }

        if (radius <= 1e-6f)
        {
            u = 0.5f + minRadius;
            v = 0.5f;
            return;
        }

        float scale = minRadius / radius;
        u = 0.5f + (deltaU * scale);
        v = 0.5f + (deltaV * scale);
    }

    static void AppendRadialSkirts(
        ref Vector3[] vertices,
        ref int[] triangles,
        ref Vector2[] geographicUvs,
        ref Vector2[] detailUvs,
        ref Color[] colors,
        int resolution,
        float skirtDepthMeters
    )
    {
        if (vertices == null ||
            triangles == null ||
            resolution < 2 ||
            skirtDepthMeters <= 0f)
        {
            return;
        }

        int gridCount = resolution * resolution;
        if (vertices.Length < gridCount)
        {
            return;
        }

        int skirtCount = resolution * 4;
        int newVertexCount = gridCount + skirtCount;
        Array.Resize(ref vertices, newVertexCount);

        if (geographicUvs != null)
        {
            Array.Resize(ref geographicUvs, newVertexCount);
        }

        if (detailUvs != null)
        {
            Array.Resize(ref detailUvs, newVertexCount);
        }

        if (colors != null)
        {
            Array.Resize(ref colors, newVertexCount);
        }

        int minVSkirt = gridCount;
        int maxVSkirt = gridCount + resolution;
        int minUSkirt = gridCount + (resolution * 2);
        int maxUSkirt = gridCount + (resolution * 3);

        for (int i = 0; i < resolution; i++)
        {
            CopySkirtVertex(
                vertices,
                geographicUvs,
                detailUvs,
                colors,
                sourceIndex: i,
                destIndex: minVSkirt + i,
                skirtDepthMeters
            );
            CopySkirtVertex(
                vertices,
                geographicUvs,
                detailUvs,
                colors,
                sourceIndex: i + ((resolution - 1) * resolution),
                destIndex: maxVSkirt + i,
                skirtDepthMeters
            );
            CopySkirtVertex(
                vertices,
                geographicUvs,
                detailUvs,
                colors,
                sourceIndex: i * resolution,
                destIndex: minUSkirt + i,
                skirtDepthMeters
            );
            CopySkirtVertex(
                vertices,
                geographicUvs,
                detailUvs,
                colors,
                sourceIndex: (i * resolution) + (resolution - 1),
                destIndex: maxUSkirt + i,
                skirtDepthMeters
            );
        }

        int extraTriangles = (resolution - 1) * 4 * 6;
        int write = triangles.Length;
        Array.Resize(ref triangles, write + extraTriangles);

        for (int i = 0; i < resolution - 1; i++)
        {
            int maxVOrig = i + ((resolution - 1) * resolution);
            int minUOrig = i * resolution;
            int maxUOrig = minUOrig + (resolution - 1);

            WriteSkirtQuadOutward(
                triangles,
                ref write,
                i,
                i + 1,
                minVSkirt + i,
                minVSkirt + i + 1
            );
            WriteSkirtQuadOutward(
                triangles,
                ref write,
                maxVOrig + 1,
                maxVOrig,
                maxVSkirt + i + 1,
                maxVSkirt + i
            );
            WriteSkirtQuadOutward(
                triangles,
                ref write,
                maxUOrig,
                maxUOrig + resolution,
                maxUSkirt + i,
                maxUSkirt + i + 1
            );
            WriteSkirtQuadOutward(
                triangles,
                ref write,
                minUOrig + resolution,
                minUOrig,
                minUSkirt + i + 1,
                minUSkirt + i
            );
        }
    }

    static void CopySkirtVertex(
        Vector3[] vertices,
        Vector2[] geographicUvs,
        Vector2[] detailUvs,
        Color[] colors,
        int sourceIndex,
        int destIndex,
        float skirtDepthMeters
    )
    {
        Vector3 source = vertices[sourceIndex];
        SimVector3 surface = new SimVector3(source.x, source.y, source.z);
        SimVector3 skirt = PlanetPqsSkirtBuilder.OffsetTowardPlanetCenter(
            surface,
            SimVector3.Zero(),
            skirtDepthMeters
        );
        vertices[destIndex] = new Vector3((float)skirt.x, (float)skirt.y, (float)skirt.z);

        if (geographicUvs != null)
        {
            geographicUvs[destIndex] = geographicUvs[sourceIndex];
        }

        if (detailUvs != null)
        {
            detailUvs[destIndex] = detailUvs[sourceIndex];
        }

        if (colors != null)
        {
            colors[destIndex] = colors[sourceIndex];
        }
    }

    static void WriteSkirtQuadOutward(
        int[] triangles,
        ref int write,
        int orig0,
        int orig1,
        int skirt0,
        int skirt1
    )
    {
        triangles[write++] = orig0;
        triangles[write++] = orig1;
        triangles[write++] = skirt1;
        triangles[write++] = orig0;
        triangles[write++] = skirt1;
        triangles[write++] = skirt0;
    }
}
