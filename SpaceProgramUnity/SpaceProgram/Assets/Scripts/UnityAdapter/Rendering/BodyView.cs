using System.Collections.Generic;
using UnityEngine;

static class BodyView
{
    const float VelocityVectorLength = 1.6f;
    const float ThrustDirectionLength = 1.1f;
    const float PlannedDeltaVVectorScale = 0.8f;
    const float RelativeVelocityVectorScale = 0.09f;
    const float MinTargetLineLength = 0.35f;
    const float MinEngineParticleRate = 18.0f;
    const float MaxEngineParticleRate = 180.0f;
    const float EngineExhaustVisualScale = 4.0f;
    const float ShipNoseLocalY = 0.72f;
    const float StationNoseLocalY = 0.80f;
    const string BlocksContainerName = "Blocks";
    const string FocusPickColliderName = "FocusPickCollider";
    const string ComDebugMarkerName = "ComDebugMarker";
    const float ComDebugMarkerScale = 1.75f;
    static readonly Color ComDebugMarkerColor = new Color(1.0f, 0.9f, 0.1f, 0.45f);
    const string MapModeMarkerName = "MapModeMarker";
    public const string MapModePickColliderName = "MapModePickCollider";
    const string LegacyDockingRingName = "DockingRing";
    const float MapMarkerScreenHeightFraction = 0.028f;

    enum MapMarkerKind
    {
        Ship,
        Station,
        Debris,
    }

    public static void UpdateVisualState(
        GameObject vesselObject,
        string bodyName,
        bool isMapModeActive
    )
    {
        if (vesselObject == null)
        {
            return;
        }

        if (bodyName != "Ship" && bodyName != "Station")
        {
            return;
        }

        MapMarkerKind markerKind = ResolveMapMarkerKind(bodyName);
        MapModeVesselMarkerOnly markerOnly = EnsureMapModeVesselMarkerOnly(vesselObject);

        if (isMapModeActive)
        {
            markerOnly.CaptureFlightRendererState();
        }

        SetChildActive(vesselObject, BlocksContainerName, !isMapModeActive);
        SetChildActive(vesselObject, FocusPickColliderName, !isMapModeActive);
        SetChildActive(vesselObject, "EngineExhaust", !isMapModeActive);
        SetChildActive(vesselObject, "Label", !isMapModeActive);
        SetVectorLineActive(vesselObject, "VelocityVector", !isMapModeActive);
        SetVectorLineActive(vesselObject, "ThrustDirection", !isMapModeActive);
        SetVectorLineActive(vesselObject, "PlannedDeltaV", !isMapModeActive);
        bool showTargetLines =
            !isMapModeActive &&
            GameViewSettings.ShowTargetDirectionLine;
        SetVectorLineActive(vesselObject, "TargetDirection", showTargetLines);
        SetVectorLineActive(vesselObject, "TargetRelativeVelocity", showTargetLines);

        if (isMapModeActive)
        {
            HideVesselDirectionVectors(vesselObject);
            HideNonMapMarkerRenderers(vesselObject);
        }
        else
        {
            markerOnly.RestoreFlightRendererState();
        }

        markerOnly.MapModeActive = isMapModeActive;

        ShipTrailView shipTrailView = vesselObject.GetComponent<ShipTrailView>();

        if (shipTrailView != null)
        {
            bool showTrail = DevSettings.ShipTrailEnabled && !isMapModeActive;
            shipTrailView.SetTrailVisible(showTrail);
        }

        GameObject mapMarker = EnsureMapModeMarker(vesselObject, markerKind);
        mapMarker.SetActive(isMapModeActive);

        if (isMapModeActive)
        {
            OrbitLineVisualUtility.ApplyOrbitOverlayLayer(mapMarker);
        }

        UpdateMapModePickCollider(vesselObject, isMapModeActive);
    }

    static MapModeVesselMarkerOnly EnsureMapModeVesselMarkerOnly(GameObject vesselObject)
    {
        MapModeVesselMarkerOnly markerOnly =
            vesselObject.GetComponent<MapModeVesselMarkerOnly>();

        if (markerOnly == null)
        {
            markerOnly = vesselObject.AddComponent<MapModeVesselMarkerOnly>();
        }

        return markerOnly;
    }

    internal static void ApplyMapModeMarkerOnly(GameObject vesselObject)
    {
        HideNonMapMarkerRenderers(vesselObject);
        HideVesselDirectionVectors(vesselObject);
    }

    static MapMarkerKind ResolveMapMarkerKind(string bodyName)
    {
        if (bodyName == "Station")
        {
            return MapMarkerKind.Station;
        }

        if (bodyName == "Ship")
        {
            return MapMarkerKind.Ship;
        }

        return MapMarkerKind.Debris;
    }

    static void SetChildActive(GameObject parentObject, string childName, bool active)
    {
        if (parentObject == null)
        {
            return;
        }

        Transform childTransform = FindDescendantTransform(
            parentObject.transform,
            childName
        );

        if (childTransform != null)
        {
            childTransform.gameObject.SetActive(active);
        }
    }

    static void SetVectorLineActive(GameObject parentObject, string lineName, bool active)
    {
        Transform lineTransform = parentObject.transform.Find(lineName);

        if (lineTransform == null)
        {
            return;
        }

        lineTransform.gameObject.SetActive(active);
        LineRenderer lineRenderer = lineTransform.GetComponent<LineRenderer>();

        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.enabled = active;

        if (!active)
        {
            lineRenderer.positionCount = 0;
        }
    }

    static void HideVesselDirectionVectors(GameObject vesselObject)
    {
        LineRenderer[] lineRenderers =
            vesselObject.GetComponentsInChildren<LineRenderer>(true);

        for (int i = 0; i < lineRenderers.Length; i++)
        {
            LineRenderer lineRenderer = lineRenderers[i];

            if (lineRenderer == null)
            {
                continue;
            }

            lineRenderer.enabled = false;
            lineRenderer.positionCount = 0;
            lineRenderer.gameObject.SetActive(false);
        }
    }

    static void HideNonMapMarkerRenderers(GameObject vesselObject)
    {
        Renderer[] renderers = vesselObject.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || IsUnderMapModeMarker(renderer.transform))
            {
                continue;
            }

            renderer.enabled = false;

            LineRenderer lineRenderer = renderer as LineRenderer;

            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
                lineRenderer.gameObject.SetActive(false);
            }
        }
    }

    static bool IsUnderMapModeMarker(Transform transform)
    {
        while (transform != null)
        {
            if (transform.name == MapModeMarkerName)
            {
                return true;
            }

            transform = transform.parent;
        }

        return false;
    }

    static GameObject EnsureMapModeMarker(GameObject vesselObject, MapMarkerKind markerKind)
    {
        Transform markerTransform = vesselObject.transform.Find(MapModeMarkerName);
        GameObject markerObject;

        if (markerTransform == null)
        {
            markerObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            markerObject.name = MapModeMarkerName;
            markerObject.transform.SetParent(vesselObject.transform, false);
            markerObject.transform.localPosition = Vector3.zero;
            markerObject.transform.localRotation = Quaternion.identity;

            Collider[] markerColliders = markerObject.GetComponents<Collider>();

            for (int i = 0; i < markerColliders.Length; i++)
            {
                Object.Destroy(markerColliders[i]);
            }

            BoxCollider markerCollider = markerObject.AddComponent<BoxCollider>();
            markerCollider.size = Vector3.one;
            markerCollider.isTrigger = false;
            Renderer markerRenderer = markerObject.GetComponent<Renderer>();

            if (markerRenderer != null)
            {
                markerRenderer.material = RuntimeMaterialFactory.CreateOrbitOverlayMarkerMaterial(
                    GetMapMarkerColor(markerKind)
                );
            }

            markerObject.AddComponent<ScreenSpaceBillboardScaler>().ScreenHeightFraction =
                MapMarkerScreenHeightFraction;
            ScreenSpaceBillboardScaler createdScaler =
                markerObject.GetComponent<ScreenSpaceBillboardScaler>();
            createdScaler.Mode = ScreenSpaceBillboardScaler.BillboardMode.CameraFacing;
        }
        else
        {
            markerObject = markerTransform.gameObject;
        }

        ScreenSpaceBillboardScaler billboardScaler =
            markerObject.GetComponent<ScreenSpaceBillboardScaler>();

        if (billboardScaler == null)
        {
            billboardScaler = markerObject.AddComponent<ScreenSpaceBillboardScaler>();
            billboardScaler.ScreenHeightFraction = MapMarkerScreenHeightFraction;
        }

        billboardScaler.Mode = ScreenSpaceBillboardScaler.BillboardMode.CameraFacing;

        markerObject.SetActive(false);
        return markerObject;
    }

    public static void UpdateMapModePickCollider(GameObject vesselObject, bool mapModeActive)
    {
        if (vesselObject == null)
        {
            return;
        }

        Transform pickTransform = vesselObject.transform.Find(MapModePickColliderName);
        GameObject pickObject;

        if (pickTransform == null)
        {
            pickObject = new GameObject(MapModePickColliderName);
            pickObject.transform.SetParent(vesselObject.transform, false);
            pickObject.AddComponent<SphereCollider>();
        }
        else
        {
            pickObject = pickTransform.gameObject;
        }

        pickObject.SetActive(mapModeActive);
        pickObject.transform.localPosition = Vector3.zero;
        pickObject.transform.localRotation = Quaternion.identity;
        pickObject.transform.localScale = Vector3.one;

        SphereCollider sphereCollider = pickObject.GetComponent<SphereCollider>();

        if (sphereCollider != null)
        {
            sphereCollider.isTrigger = false;
            sphereCollider.radius = 20f;
        }

        if (mapModeActive && FlightRenderLayers.HasDedicatedLayers())
        {
            FlightRenderLayers.ApplyScaledSpaceLayer(pickObject);
        }
    }

    static Color GetMapMarkerColor(MapMarkerKind markerKind)
    {
        switch (markerKind)
        {
            case MapMarkerKind.Station:
                return new Color(1.0f, 0.72f, 0.18f, 0.95f);
            case MapMarkerKind.Debris:
                return new Color(0.72f, 0.72f, 0.72f, 0.95f);
            default:
                return new Color(0.18f, 0.95f, 0.42f, 0.95f);
        }
    }

    public static void SetComDebugMarker(GameObject shipObject, bool visible)
    {
        if (shipObject == null)
        {
            return;
        }

        Transform markerTransform = shipObject.transform.Find(ComDebugMarkerName);

        if (markerTransform == null)
        {
            GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerObject.name = ComDebugMarkerName;
            markerObject.transform.SetParent(shipObject.transform, false);
            markerObject.transform.localPosition = Vector3.zero;
            Object.Destroy(markerObject.GetComponent<Collider>());
            markerTransform = markerObject.transform;
            markerObject.SetActive(false);
        }

        markerTransform.localScale = Vector3.one * ComDebugMarkerScale;
        ApplyComDebugMarkerAppearance(markerTransform.gameObject);
        markerTransform.gameObject.SetActive(visible);
    }

    static void ApplyComDebugMarkerAppearance(GameObject markerObject)
    {
        Renderer renderer = markerObject.GetComponent<Renderer>();

        if (renderer == null)
        {
            return;
        }

        Material material = RuntimeMaterialFactory.CreateTransparentSolidMaterial(
            ComDebugMarkerColor
        );

        if (material != null)
        {
            renderer.material = material;
        }
    }

    public static double GetDockingPortOffsetSimUnits(string bodyName)
    {
        return bodyName == "Station" ?
            StationNoseLocalY :
            ShipNoseLocalY;
    }

    public static Vector3 ToUnityVesselLocalOffset(global::SimVector3 simLocalOffset)
    {
        return new Vector3(
            (float)simLocalOffset.x,
            (float)simLocalOffset.y,
            (float)simLocalOffset.z
        );
    }

    public static void ApplyVesselWorldCenterOfMassPosition(
        GameObject vesselObject,
        global::SimVector3 centerOfMassWorld,
        global::SimVector3 renderOrigin,
        float positionScale
    )
    {
        if (vesselObject == null)
        {
            return;
        }

        vesselObject.transform.position = WorldVisualBridge.SimToUnityPosition(
            centerOfMassWorld,
            renderOrigin,
            positionScale
        );
    }

    public static void ApplyBlocksContainerCenterOfMassOffset(
        GameObject vesselObject,
        global::SimVector3 centerOfMassLocal
    )
    {
        if (vesselObject == null)
        {
            return;
        }

        Transform blocksContainer = vesselObject.transform.Find(BlocksContainerName);

        if (blocksContainer == null)
        {
            return;
        }

        blocksContainer.localPosition = ToUnityVesselLocalOffset(
            centerOfMassLocal.Multiply(-1.0)
        );
    }

    public static GameObject Create(string bodyName)
    {
        if (bodyName == "Ship" || bodyName == "Station")
        {
            return CreateVessel(bodyName);
        }

        GameObject bodyObject = CreateCelestialBodyObject(bodyName);

        if (bodyName == "Sun")
        {
            SetColor(bodyObject, WorldVisualBridge.MapSunDiscColor);
        }

        return bodyObject;
    }

    /// <summary>
    /// After SoI LOD swap: the new body gets the quadtree, the old one becomes a distant globe.
    /// </summary>
    public static void RebindLodGlobes(IReadOnlyDictionary<string, GameObject> bodyObjects)
    {
        if (bodyObjects == null)
        {
            return;
        }

        RebindLodGlobe(bodyObjects, CelestialTerrainRuntime.EarthBodyName);
        RebindLodGlobe(bodyObjects, CelestialTerrainRuntime.MoonBodyName);
    }

    static void RebindLodGlobe(
        IReadOnlyDictionary<string, GameObject> bodyObjects,
        string bodyName
    )
    {
        if (!bodyObjects.TryGetValue(bodyName, out GameObject bodyObject) || bodyObject == null)
        {
            return;
        }

        bool isLod = bodyName == CelestialTerrainRuntime.LodBodyName;
        FlightPlanetSphere sphere = bodyObject.GetComponent<FlightPlanetSphere>();
        DistantCelestialGlobe globe = bodyObject.GetComponent<DistantCelestialGlobe>();

        if (isLod)
        {
            if (globe != null)
            {
                UnityEngine.Object.DestroyImmediate(globe);
            }

            Transform leftoverDepth = bodyObject.transform.Find("PlanetGlobeDepth");

            if (leftoverDepth != null)
            {
                UnityEngine.Object.DestroyImmediate(leftoverDepth.gameObject);
            }

            FlightPlanetMapSphere rootMap = bodyObject.GetComponent<FlightPlanetMapSphere>();

            if (rootMap != null)
            {
                UnityEngine.Object.DestroyImmediate(rootMap);
            }

            if (sphere == null)
            {
                FlightPlanetSphere.AttachTo(bodyObject);
            }
            else
            {
                sphere.ResumeFromHibernate();
            }

            return;
        }

        if (sphere != null)
        {
            sphere.HibernateForDistantGlobe();
        }
        else
        {
            FlightPlanetSphere.StripLodHierarchy(bodyObject);
        }

        DistantCelestialGlobe.Attach(bodyObject, bodyName);
    }

    const int CelestialIcosphereSubdivisions = 4;

    static GameObject CreateCelestialBodyObject(string bodyName)
    {
        GameObject bodyObject = new GameObject(bodyName);

        if (bodyName == CelestialTerrainRuntime.LodBodyName)
        {
            FlightPlanetSphere.AttachTo(bodyObject);
            return bodyObject;
        }

        if (bodyName == "Planet" || bodyName == "Moon")
        {
            DistantCelestialGlobe.Attach(bodyObject, bodyName);
            return bodyObject;
        }

        MeshFilter meshFilter = bodyObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = bodyObject.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = BuildIcosphereMesh(CelestialIcosphereSubdivisions, 1.0f);
        bodyObject.transform.localScale = Vector3.one;

        if (meshRenderer.sharedMaterial == null)
        {
            meshRenderer.material = RuntimeMaterialFactory.CreateSolidMaterial(
                Color.white
            );
        }

        return bodyObject;
    }

    static Mesh BuildIcosphereMesh(int subdivisions, float radius, float launchSiteFlattenRadius = 0f)
    {
        int clampedSubdivisions = Mathf.Clamp(subdivisions, 0, 5);
        float meshRadius = Mathf.Max(0.001f, radius);
        float goldenRatio = (1.0f + Mathf.Sqrt(5.0f)) * 0.5f;
        var vertices = new List<Vector3>(12)
        {
            NormalizeVertex(new Vector3(-1.0f, goldenRatio, 0.0f)),
            NormalizeVertex(new Vector3(1.0f, goldenRatio, 0.0f)),
            NormalizeVertex(new Vector3(-1.0f, -goldenRatio, 0.0f)),
            NormalizeVertex(new Vector3(1.0f, -goldenRatio, 0.0f)),
            NormalizeVertex(new Vector3(0.0f, -1.0f, goldenRatio)),
            NormalizeVertex(new Vector3(0.0f, 1.0f, goldenRatio)),
            NormalizeVertex(new Vector3(0.0f, -1.0f, -goldenRatio)),
            NormalizeVertex(new Vector3(0.0f, 1.0f, -goldenRatio)),
            NormalizeVertex(new Vector3(goldenRatio, 0.0f, -1.0f)),
            NormalizeVertex(new Vector3(goldenRatio, 0.0f, 1.0f)),
            NormalizeVertex(new Vector3(-goldenRatio, 0.0f, -1.0f)),
            NormalizeVertex(new Vector3(-goldenRatio, 0.0f, 1.0f)),
        };

        var triangles = new List<int>
        {
            0, 11, 5,
            0, 5, 1,
            0, 1, 7,
            0, 7, 10,
            0, 10, 11,
            1, 5, 9,
            5, 11, 4,
            11, 10, 2,
            10, 7, 6,
            7, 1, 8,
            3, 9, 4,
            3, 4, 2,
            3, 2, 6,
            3, 6, 8,
            3, 8, 9,
            4, 9, 5,
            2, 4, 11,
            6, 2, 10,
            8, 6, 7,
            9, 8, 1,
        };

        var midpointCache = new Dictionary<long, int>();

        for (int i = 0; i < clampedSubdivisions; i++)
        {
            var nextTriangles = new List<int>(triangles.Count * 4);

            for (int t = 0; t < triangles.Count; t += 3)
            {
                int v0 = triangles[t];
                int v1 = triangles[t + 1];
                int v2 = triangles[t + 2];
                int a = GetIcosphereMidpoint(v0, v1, vertices, midpointCache);
                int b = GetIcosphereMidpoint(v1, v2, vertices, midpointCache);
                int c = GetIcosphereMidpoint(v2, v0, vertices, midpointCache);

                nextTriangles.Add(v0);
                nextTriangles.Add(a);
                nextTriangles.Add(c);
                nextTriangles.Add(v1);
                nextTriangles.Add(b);
                nextTriangles.Add(a);
                nextTriangles.Add(v2);
                nextTriangles.Add(c);
                nextTriangles.Add(b);
                nextTriangles.Add(a);
                nextTriangles.Add(b);
                nextTriangles.Add(c);
            }

            triangles = nextTriangles;
        }

        for (int i = 0; i < vertices.Count; i++)
        {
            vertices[i] *= meshRadius;
        }

        if (launchSiteFlattenRadius > 0f)
        {
            ApplyLaunchSiteFlatten(vertices, meshRadius, launchSiteFlattenRadius);
        }

        Mesh mesh = new Mesh
        {
            name = "Icosphere",
            indexFormat = triangles.Count > 65535 ?
                UnityEngine.Rendering.IndexFormat.UInt32 :
                UnityEngine.Rendering.IndexFormat.UInt16,
        };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void ApplyLaunchSiteFlatten(
        List<Vector3> vertices,
        float planetRadius,
        float flattenRadius
    )
    {
        float clampedFlattenRadius = Mathf.Max(1f, flattenRadius);

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 curved = vertices[i];
            float horizontalDistance = Mathf.Sqrt(
                curved.x * curved.x + curved.z * curved.z
            );

            if (horizontalDistance >= clampedFlattenRadius)
            {
                continue;
            }

            float blend = Mathf.SmoothStep(1f, 0f, horizontalDistance / clampedFlattenRadius);
            Vector3 flat = new Vector3(curved.x, planetRadius, curved.z);
            vertices[i] = Vector3.Lerp(curved, flat, blend);
        }
    }

    static Vector3 NormalizeVertex(Vector3 vertex)
    {
        return vertex.normalized;
    }

    static int GetIcosphereMidpoint(
        int indexA,
        int indexB,
        List<Vector3> vertices,
        Dictionary<long, int> midpointCache
    )
    {
        long cacheKey = ((long)Mathf.Min(indexA, indexB) << 32) +
            (uint)Mathf.Max(indexA, indexB);

        if (midpointCache.TryGetValue(cacheKey, out int cachedIndex))
        {
            return cachedIndex;
        }

        Vector3 midpoint = NormalizeVertex(
            (vertices[indexA] + vertices[indexB]) * 0.5f
        );
        int midpointIndex = vertices.Count;
        vertices.Add(midpoint);
        midpointCache[cacheKey] = midpointIndex;
        return midpointIndex;
    }

    static GameObject CreateVessel(string bodyName)
    {
        GameObject vesselObject = new GameObject(bodyName);

        GameObject blocksContainer = new GameObject(BlocksContainerName);
        blocksContainer.transform.SetParent(vesselObject.transform);
        blocksContainer.transform.localPosition = Vector3.zero;
        blocksContainer.transform.localRotation = Quaternion.identity;
        blocksContainer.transform.localScale = Vector3.one;

        bool isStation = bodyName == "Station";
        Color velocityColor = isStation ?
            new Color(1.0f, 0.7f, 0.2f) :
            new Color(0.1f, 1.0f, 0.2f);
        CreateVectorLine(vesselObject, "VelocityVector", velocityColor);

        if (isStation)
        {
            CreateStationLabel(vesselObject, bodyName);
        }
        else
        {
            CreateVectorLine(vesselObject, "ThrustDirection", new Color(1.0f, 0.55f, 0.1f));
            CreateVectorLine(vesselObject, "PlannedDeltaV", new Color(1.0f, 0.85f, 0.0f));
            CreateVectorLine(vesselObject, "TargetDirection", new Color(1.0f, 0.25f, 0.9f));
            CreateVectorLine(
                vesselObject,
                "TargetRelativeVelocity",
                new Color(0.55f, 0.8f, 1.0f)
            );
            vesselObject.AddComponent<ShipTrailView>();
        }

        CreateEngineExhaust(vesselObject);
        StripLegacyDockingRing(vesselObject);
        StripLegacyCapsuleVisual(vesselObject);

        vesselObject.transform.localScale = Vector3.one;

        return vesselObject;
    }

    public static void EnforceVesselScaleIsolation(GameObject vesselObject)
    {
        if (vesselObject == null)
        {
            return;
        }

        vesselObject.transform.localScale = Vector3.one;

        Transform blocksContainer = vesselObject.transform.Find(BlocksContainerName);

        if (blocksContainer != null)
        {
            blocksContainer.localScale = Vector3.one;
        }
    }

    public static void EnsureVesselUnityScale(GameObject vesselObject)
    {
        if (vesselObject == null)
        {
            return;
        }

        vesselObject.transform.localScale = Vector3.one;

        Transform blocksContainer = vesselObject.transform.Find(BlocksContainerName);

        if (blocksContainer != null)
        {
            blocksContainer.localScale = Vector3.one;
        }
    }

    public static void EnsureVesselVisualAttachments(GameObject vesselObject, string bodyName)
    {
        if (vesselObject == null)
        {
            return;
        }

        StripLegacyCapsuleVisual(vesselObject);
        StripLegacyCockpitMarkers(vesselObject);

        if (vesselObject.transform.Find(BlocksContainerName) == null)
        {
            GameObject blocksContainer = new GameObject(BlocksContainerName);
            blocksContainer.transform.SetParent(vesselObject.transform, false);
            blocksContainer.transform.localPosition = Vector3.zero;
            blocksContainer.transform.localRotation = Quaternion.identity;
            blocksContainer.transform.localScale = Vector3.one;
        }

        if (vesselObject.transform.Find("EngineExhaust") == null)
        {
            CreateEngineExhaust(vesselObject);
        }

        StripLegacyDockingRing(vesselObject);
    }

    public static void UpdateFocusPickCollider(
        GameObject vesselObject,
        float radiusWorld
    )
    {
        if (vesselObject == null)
        {
            return;
        }

        Transform pickTransform = vesselObject.transform.Find(FocusPickColliderName);
        GameObject pickObject;

        if (pickTransform == null)
        {
            pickObject = new GameObject(FocusPickColliderName);
            pickObject.transform.SetParent(vesselObject.transform, false);
            pickObject.AddComponent<SphereCollider>();
        }
        else
        {
            pickObject = pickTransform.gameObject;
        }

        float radius = Mathf.Max(0.25f, radiusWorld);
        pickObject.transform.localPosition = Vector3.zero;
        pickObject.transform.localRotation = Quaternion.identity;
        pickObject.transform.localScale = Vector3.one;

        SphereCollider sphereCollider = pickObject.GetComponent<SphereCollider>();

        if (sphereCollider != null)
        {
            sphereCollider.isTrigger = false;
            sphereCollider.radius = radius;
        }
    }

    static void StripLegacyDockingRing(GameObject vesselObject)
    {
        if (vesselObject == null)
        {
            return;
        }

        Transform legacyRing = vesselObject.transform.Find(LegacyDockingRingName);

        if (legacyRing != null)
        {
            Object.Destroy(legacyRing.gameObject);
        }
    }

    public static void StripLegacyCapsuleVisual(GameObject vesselObject)
    {
        if (vesselObject == null)
        {
            return;
        }

        string[] legacyChildNames = { "Hull", "Nose" };

        for (int i = 0; i < legacyChildNames.Length; i++)
        {
            Transform legacyChild = vesselObject.transform.Find(legacyChildNames[i]);

            if (legacyChild != null)
            {
                Object.Destroy(legacyChild.gameObject);
            }
        }
    }

    static void StripLegacyCockpitMarkers(GameObject vesselObject)
    {
        if (vesselObject == null)
        {
            return;
        }

        string[] legacyMarkerNames =
        {
            "CockpitFuselageMarker",
            "CockpitOrientationMarker",
        };

        for (int i = 0; i < legacyMarkerNames.Length; i++)
        {
            Transform legacyMarker = vesselObject.transform.Find(legacyMarkerNames[i]);

            if (legacyMarker != null)
            {
                Object.Destroy(legacyMarker.gameObject);
            }
        }
    }

    public static void RebuildBlocks(
        GameObject shipObject,
        ShipBlockView[] blocks,
        global::SimVector3 centerOfMassLocal,
        bool anchorVesselRootAtCom = false
    )
    {
        EnsureVesselUnityScale(shipObject);

        Transform blocksContainer = shipObject.transform.Find(BlocksContainerName);

        if (blocksContainer == null)
        {
            GameObject containerObject = new GameObject(BlocksContainerName);
            containerObject.transform.SetParent(shipObject.transform);
            containerObject.transform.localPosition = Vector3.zero;
            containerObject.transform.localRotation = Quaternion.identity;
            containerObject.transform.localScale = Vector3.one;
            blocksContainer = containerObject.transform;
        }

        for (int i = blocksContainer.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(blocksContainer.GetChild(i).gameObject);
        }

        if (blocks == null)
        {
            return;
        }

        Transform partTree = FlightVesselSpawner.RebuildPartTree(blocksContainer, blocks);

        for (int i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].blockType == ShipBlockType.DockingPort)
            {
                continue;
            }

            if (blocks[i].hasVabAttach && !string.IsNullOrWhiteSpace(blocks[i].partTemplateId))
            {
                continue;
            }

            CreateAndParentBlock(blocksContainer, blocks[i], centerOfMassLocal);
        }

        for (int i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].blockType != ShipBlockType.DockingPort)
            {
                continue;
            }

            CreateAndParentBlock(blocksContainer, blocks[i], centerOfMassLocal);
        }

        ApplyVesselBlockLayoutFromSimMeters(
            shipObject,
            blocks,
            centerOfMassLocal,
            partTree,
            anchorVesselRootAtCom
        );
        PruneStaleBlockVisuals(shipObject, blocks);
    }

    static void AlignBlocksContainerHorizontallyOnPad(Transform blocksContainer)
    {
        if (blocksContainer == null)
        {
            return;
        }

        Renderer[] renderers = blocksContainer.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer != null)
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        Vector3 centerLocal = blocksContainer.InverseTransformPoint(bounds.center);
        Vector3 localPosition = blocksContainer.localPosition;
        localPosition.x -= centerLocal.x;
        localPosition.z -= centerLocal.z;
        blocksContainer.localPosition = localPosition;
    }

    static Transform FindFlatBlockTransform(
        Transform blocksContainer,
        string blockName,
        Transform partTree
    )
    {
        if (blocksContainer == null || string.IsNullOrEmpty(blockName))
        {
            return null;
        }

        for (int i = 0; i < blocksContainer.childCount; i++)
        {
            Transform child = blocksContainer.GetChild(i);

            if (child == null || child == partTree)
            {
                continue;
            }

            if (child.name == blockName)
            {
                return child;
            }
        }

        return null;
    }

    static void CreateAndParentBlock(
        Transform blocksContainer,
        ShipBlockView block,
        global::SimVector3 centerOfMassLocal
    )
    {
        if (block.blockType == ShipBlockType.RcsThruster)
        {
            return;
        }

        GameObject blockObject;

        if (!FlightPartVisualFactory.TryCreatePartVisual(block.partTemplateId, out blockObject))
        {
            blockObject = BlockMeshFactory.CreateBlock(block);
            blockObject.transform.SetParent(blocksContainer, false);
            BlockMeshFactory.ApplyBlockPositionFromCom(
                blockObject.transform,
                block,
                centerOfMassLocal
            );

            if (!BlockMeshFactory.UsesIntrinsicBlockScale(block.blockType))
            {
                blockObject.transform.localScale = BlockMeshFactory.ToUnityLocalScale(
                    block.dimensions
                );
            }

            return;
        }

        blockObject.transform.SetParent(blocksContainer, false);
        blockObject.transform.localPosition = BlockMeshFactory.ToUnityLocalPosition(
            block.localOffset.Subtract(centerOfMassLocal)
        );
        blockObject.transform.localRotation = Quaternion.identity;
        blockObject.transform.localScale = Vector3.one;
    }

    public static void UpdateBlockTransforms(
        GameObject shipObject,
        ShipBlockView[] blocks,
        global::SimVector3 centerOfMassLocal,
        bool anchorVesselRootAtCom = false
    )
    {
        ApplyVesselBlockLayoutFromSimMeters(
            shipObject,
            blocks,
            centerOfMassLocal,
            null,
            anchorVesselRootAtCom
        );
        PruneStaleBlockVisuals(shipObject, blocks);
    }

    /// <summary>
    /// Positions vessel blocks in honest 1:1 sim meters under the Blocks container.
    /// Never applies worldScale here — only the vessel root world position is scaled.
    /// </summary>
    public static void ApplyVesselBlockLayoutFromSimMeters(
        GameObject shipObject,
        ShipBlockView[] blocks,
        global::SimVector3 centerOfMassLocalSim
    )
    {
        Transform partTree = null;
        Transform blocksContainer = shipObject != null ?
            shipObject.transform.Find(BlocksContainerName) :
            null;

        if (blocksContainer != null)
        {
            Transform existingTree = blocksContainer.Find(FlightVesselSpawner.PartTreeName);

            if (existingTree != null)
            {
                partTree = existingTree;
            }
        }

        ApplyVesselBlockLayoutFromSimMeters(
            shipObject,
            blocks,
            centerOfMassLocalSim,
            partTree
        );
    }

    public static void ApplyVesselBlockLayoutFromSimMeters(
        GameObject shipObject,
        ShipBlockView[] blocks,
        global::SimVector3 centerOfMassLocalSim,
        Transform partTree
    )
    {
        ApplyVesselBlockLayoutFromSimMeters(
            shipObject,
            blocks,
            centerOfMassLocalSim,
            partTree,
            anchorVesselRootAtCom: false
        );
    }

    public static void ApplyVesselBlockLayoutFromSimMeters(
        GameObject shipObject,
        ShipBlockView[] blocks,
        global::SimVector3 centerOfMassLocalSim,
        Transform partTree,
        bool anchorVesselRootAtCom
    )
    {
        EnsureVesselUnityScale(shipObject);
        EnforceVesselScaleIsolation(shipObject);

        Transform blocksContainer = shipObject.transform.Find(BlocksContainerName);

        if (blocksContainer == null || blocks == null)
        {
            return;
        }

        blocksContainer.localScale = Vector3.one;

        if (partTree != null && FlightVesselSpawner.HasVabHierarchy(blocks))
        {
            blocksContainer.localPosition = Vector3.zero;
            global::SimVector3 attachFrameOriginOffset =
                GetVabAttachFrameOriginOffset(blocks);
            global::SimVector3 comInAttachFrame = centerOfMassLocalSim
                .Subtract(attachFrameOriginOffset);
            partTree.localPosition = ToUnityVesselLocalOffset(
                comInAttachFrame.Multiply(-1.0)
            );
        }
        else
        {
            blocksContainer.localPosition = anchorVesselRootAtCom ?
                Vector3.zero :
                ToUnityVesselLocalOffset(centerOfMassLocalSim.Multiply(-1.0));
        }

        for (int i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].hasVabAttach && !string.IsNullOrWhiteSpace(blocks[i].partTemplateId))
            {
                continue;
            }

            string blockName = BlockMeshFactory.GetBlockObjectName(blocks[i]);
            Transform blockTransform = FindFlatBlockTransform(
                blocksContainer,
                blockName,
                partTree
            );

            if (blockTransform == null)
            {
                continue;
            }

            global::SimVector3 offsetFromComSim = blocks[i].localOffset.Subtract(
                centerOfMassLocalSim
            );
            blockTransform.localPosition = BlockMeshFactory.ToUnityLocalPosition(
                offsetFromComSim
            );

            if (BlockMeshFactory.UsesIntrinsicBlockScale(blocks[i].blockType))
            {
                blockTransform.localScale = Vector3.one;
            }
            else
            {
                blockTransform.localScale = BlockMeshFactory.ToUnityLocalScale(
                    blocks[i].dimensions
                );
            }
        }
    }

    public static void SyncEngineExhaustToActiveThrust(
        GameObject shipObject,
        ShipBlockView[] blocks,
        global::SimVector3 centerOfMassLocal
    )
    {
        if (shipObject == null)
        {
            return;
        }

        Transform blocksContainer = shipObject.transform.Find(BlocksContainerName);
        Transform partTree = blocksContainer != null ?
            blocksContainer.Find(FlightVesselSpawner.PartTreeName) :
            null;

        UpdateEngineExhaustPosition(
            shipObject,
            blocks,
            centerOfMassLocal,
            partTree
        );
    }

    public static bool TryGetVesselVisualPivotWorld(
        GameObject vesselObject,
        out Vector3 pivotWorldPosition
    )
    {
        pivotWorldPosition = vesselObject != null ?
            vesselObject.transform.position :
            Vector3.zero;

        if (vesselObject == null)
        {
            return false;
        }

        Transform blocksContainer = vesselObject.transform.Find(BlocksContainerName);
        Transform searchRoot = blocksContainer != null ?
            blocksContainer :
            vesselObject.transform;
        Renderer[] renderers = searchRoot.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            return false;
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer != null)
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        pivotWorldPosition = bounds.center;
        return true;
    }

    /// <summary>
    /// Shift the Blocks container so the lowest <c>Node_Bottom</c> sits on
    /// <paramref name="spawnWorldPosition"/> (pad SpawnPoint). Call after layout,
    /// vessel rotation, and pad world transform. Layout zeros the offset each Update.
    /// </summary>
    public static bool TryAlignVesselVisualToWorldSpawnPoint(
        GameObject vesselObject,
        Vector3 spawnWorldPosition
    )
    {
        if (vesselObject == null)
        {
            return false;
        }

        Transform blocksContainer = vesselObject.transform.Find(BlocksContainerName);

        if (blocksContainer == null)
        {
            return false;
        }

        Transform shipRoot = blocksContainer.parent != null ?
            blocksContainer.parent :
            vesselObject.transform;
        Vector3 up = shipRoot.up;
        Vector3 origin = shipRoot.position;
        Vector3 feetWorld;

        if (
            !FlightVesselSpawner.TryGetLowestBottomAttachNodeWorldPosition(
                blocksContainer,
                out feetWorld
            ) &&
            !FlightVesselSpawner.TryGetLowestRendererWorldPositionAlongAxis(
                blocksContainer,
                up,
                origin,
                out feetWorld
            )
        )
        {
            return false;
        }

        Vector3 spawnLocal = shipRoot.InverseTransformPoint(spawnWorldPosition);
        Vector3 feetLocal = shipRoot.InverseTransformPoint(feetWorld);
        Vector3 deltaLocal = spawnLocal - feetLocal;

        if (deltaLocal.sqrMagnitude > 1e-12f)
        {
            blocksContainer.localPosition += deltaLocal;
        }

        return true;
    }

    static void UpdateEngineExhaustPosition(
        GameObject shipObject,
        ShipBlockView[] blocks,
        global::SimVector3 centerOfMassLocal,
        Transform partTree = null
    )
    {
        Transform exhaustTransform = FindDescendantTransform(
            shipObject.transform,
            "EngineExhaust"
        );
        Transform blocksContainer = shipObject.transform.Find(BlocksContainerName);
        Transform shipRoot = shipObject.transform;

        if (exhaustTransform == null)
        {
            return;
        }

        if (exhaustTransform.parent != shipRoot)
        {
            exhaustTransform.SetParent(shipRoot, false);
        }

        Transform thrustSource = TryFindActiveEngineThrustTransform(
            blocksContainer,
            partTree,
            blocks
        );

        if (thrustSource != null)
        {
            if (exhaustTransform.parent != thrustSource)
            {
                exhaustTransform.SetParent(thrustSource, worldPositionStays: false);
            }

            exhaustTransform.localPosition = Vector3.zero;
            exhaustTransform.localRotation = Quaternion.FromToRotation(
                Vector3.forward,
                Vector3.up
            );
            return;
        }

        if (blocks == null)
        {
            exhaustTransform.localPosition = new Vector3(0.0f, 0.0f, -0.65f);
            exhaustTransform.localRotation = EngineExhaustLocalRotation;
            return;
        }

        bool foundEngine = false;
        int bestStageIndex = int.MaxValue;
        Vector3 bestLocalPosition = Vector3.zero;
        Quaternion bestLocalRotation = EngineExhaustLocalRotation;

        for (int i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].blockType != ShipBlockType.Engine)
            {
                continue;
            }

            if (blocks[i].stageIndex > bestStageIndex)
            {
                continue;
            }

            global::SimVector3 thrustPointFromCom = blocks[i].localOffset
                .Add(blocks[i].thrustLocalOffset)
                .Subtract(centerOfMassLocal);
            Vector3 thrustLocalPosition = BlockMeshFactory.ToUnityLocalPosition(
                thrustPointFromCom
            );
            global::SimVector3 nozzleOffsetSim = blocks[i].thrustLocalOffset.LengthSquared() > 1e-12 ?
                global::SimVector3.Zero() :
                new global::SimVector3(0.0, 0.0, -blocks[i].dimensions.z * 0.5);
            Vector3 nozzleOffsetUnity = BlockMeshFactory.ToUnityLocalPosition(
                nozzleOffsetSim
            );

            bestStageIndex = blocks[i].stageIndex;
            bestLocalPosition = thrustLocalPosition + nozzleOffsetUnity;
            bestLocalRotation = EngineExhaustLocalRotation;
            foundEngine = true;
        }

        if (!foundEngine)
        {
            exhaustTransform.localPosition = new Vector3(0.0f, 0.0f, -0.65f);
            exhaustTransform.localRotation = EngineExhaustLocalRotation;
            return;
        }

        exhaustTransform.localPosition = bestLocalPosition;
        exhaustTransform.localRotation = bestLocalRotation;
    }

    static Transform TryFindActiveEngineThrustTransform(
        Transform blocksContainer,
        Transform partTree,
        ShipBlockView[] blocks
    )
    {
        if (blocksContainer == null || blocks == null)
        {
            return null;
        }

        int bestStageIndex = int.MaxValue;
        Transform bestThrustTransform = null;

        for (int i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].blockType != ShipBlockType.Engine)
            {
                continue;
            }

            if (blocks[i].stageIndex > bestStageIndex)
            {
                continue;
            }

            Transform partRoot = FindBlockPartTransform(
                blocksContainer,
                partTree,
                blocks[i]
            );

            if (partRoot == null)
            {
                continue;
            }

            Transform thrustTransform = PartPrefabBuilder.FindThrustTransform(partRoot);

            if (thrustTransform == null)
            {
                continue;
            }

            bestStageIndex = blocks[i].stageIndex;
            bestThrustTransform = thrustTransform;
        }

        return bestThrustTransform;
    }

    static Transform FindBlockPartTransform(
        Transform blocksContainer,
        Transform partTree,
        ShipBlockView block
    )
    {
        if (blocksContainer == null || block.blockType != ShipBlockType.Engine)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(block.id))
        {
            Transform partTransform = FindDescendantTransform(
                partTree != null ? partTree : blocksContainer,
                block.id
            );

            if (partTransform != null)
            {
                return partTransform;
            }
        }

        string blockName = BlockMeshFactory.GetBlockObjectName(block);
        return FindFlatBlockTransform(blocksContainer, blockName, partTree);
    }

    static global::SimVector3 GetVabAttachFrameOriginOffset(ShipBlockView[] blocks)
    {
        if (blocks == null)
        {
            return global::SimVector3.Zero();
        }

        for (int i = 0; i < blocks.Length; i++)
        {
            ShipBlockView block = blocks[i];

            if (
                !block.hasVabAttach ||
                string.IsNullOrWhiteSpace(block.partTemplateId) ||
                !string.IsNullOrWhiteSpace(block.parentBlockId)
            )
            {
                continue;
            }

            return block.localOffset.Subtract(block.attachLocalPosition);
        }

        return global::SimVector3.Zero();
    }

    static Transform FindDescendantTransform(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child == null)
            {
                continue;
            }

            Transform found = FindDescendantTransform(child, childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static void CreateStationLabel(GameObject stationObject, string labelText)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(stationObject.transform);
        labelObject.transform.localPosition = new Vector3(0, 0.92f, 0);
        labelObject.transform.localRotation = Quaternion.identity;

        TextMesh textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = labelText;
        textMesh.characterSize = 0.065f;
        textMesh.fontSize = 32;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(1.0f, 0.78f, 0.25f);
    }

    public static Quaternion RotationFromHeadingDegrees(float headingDegrees)
    {
        return OrientationToUnityRotation(
            global::SimQuaternion.FromYawRadians(headingDegrees * Mathf.Deg2Rad)
        );
    }

    public static Vector3 ToUnityWorldDirection(global::SimVector3 simDirection)
    {
        return new Vector3(
            (float)simDirection.x,
            (float)simDirection.y,
            (float)simDirection.z
        );
    }

    public static Quaternion OrientationToUnityRotation(global::SimQuaternion orientation)
    {
        Quaternion unityRotation = new Quaternion(
            (float)orientation.x,
            (float)orientation.y,
            (float)orientation.z,
            (float)orientation.w
        );

        float magnitudeSquared =
            unityRotation.x * unityRotation.x +
            unityRotation.y * unityRotation.y +
            unityRotation.z * unityRotation.z +
            unityRotation.w * unityRotation.w;

        if (magnitudeSquared <= 1e-8f)
        {
            return Quaternion.identity;
        }

        return unityRotation;
    }

    static readonly Quaternion EngineExhaustLocalRotation =
        Quaternion.LookRotation(Vector3.back, Vector3.up);

    public static Transform GetVesselOrientationTransform(GameObject vesselObject)
    {
        if (vesselObject == null)
        {
            return null;
        }

        Transform blocksContainer = vesselObject.transform.Find(BlocksContainerName);
        return blocksContainer != null ? blocksContainer : vesselObject.transform;
    }

    public static Vector3 GetVesselCenterOfMassWorldPosition(
        GameObject vesselObject,
        global::SimVector3 centerOfMassLocalSim,
        bool anchorVesselRootAtCom
    )
    {
        if (vesselObject == null)
        {
            return Vector3.zero;
        }

        if (anchorVesselRootAtCom)
        {
            return vesselObject.transform.position;
        }

        Transform orientationTransform = GetVesselOrientationTransform(vesselObject);

        if (orientationTransform == null)
        {
            return vesselObject.transform.position;
        }

        return orientationTransform.TransformPoint(
            ToUnityVesselLocalOffset(centerOfMassLocalSim)
        );
    }

    public static void ApplyFlightVesselFrameZero(
        GameObject vesselObject,
        global::SimQuaternion previousOrientation,
        global::SimQuaternion targetOrientation,
        float interpolationT,
        Vector3 surfaceNormalUnity
    )
    {
        if (vesselObject == null)
        {
            return;
        }

        vesselObject.transform.localPosition = Vector3.zero;
        vesselObject.transform.localRotation = Quaternion.identity;

        Transform orientationTransform = GetVesselOrientationTransform(vesselObject);

        if (orientationTransform == null)
        {
            return;
        }

        Quaternion targetRotation = WorldVisualBridge.SimOrientationToFlightVisualRotation(
            targetOrientation,
            surfaceNormalUnity
        );

        if (interpolationT >= 1.0f)
        {
            orientationTransform.localRotation = targetRotation;
            return;
        }

        Quaternion previousRotation = WorldVisualBridge.SimOrientationToFlightVisualRotation(
            previousOrientation,
            surfaceNormalUnity
        );
        orientationTransform.localRotation = SlerpRotation(
            previousRotation,
            targetRotation,
            interpolationT
        );
    }

    public static void ApplyVesselOrientation(
        Transform vesselTransform,
        global::SimQuaternion previousOrientation,
        global::SimQuaternion targetOrientation,
        float interpolationT
    )
    {
        if (vesselTransform == null)
        {
            return;
        }

        Quaternion targetRotation = OrientationToUnityRotation(targetOrientation);

        if (interpolationT >= 1.0f)
        {
            vesselTransform.rotation = targetRotation;
            return;
        }

        Quaternion previousRotation = OrientationToUnityRotation(previousOrientation);
        vesselTransform.rotation = SlerpRotation(
            previousRotation,
            targetRotation,
            interpolationT
        );
    }

    public static Quaternion SlerpRotation(
        Quaternion fromRotation,
        Quaternion toRotation,
        float interpolationT
    )
    {
        if (Quaternion.Dot(fromRotation, toRotation) < 0.0f)
        {
            toRotation = new Quaternion(
                -toRotation.x,
                -toRotation.y,
                -toRotation.z,
                -toRotation.w
            );
        }

        return Quaternion.Slerp(fromRotation, toRotation, interpolationT);
    }

    public static void ApplyVesselRotation(
        Transform vesselTransform,
        float previousHeadingDegrees,
        float targetHeadingDegrees,
        float interpolationT
    )
    {
        if (vesselTransform == null)
        {
            return;
        }

        Quaternion targetRotation = RotationFromHeadingDegrees(targetHeadingDegrees);

        if (interpolationT >= 1.0f)
        {
            vesselTransform.rotation = targetRotation;
            return;
        }

        Quaternion previousRotation = RotationFromHeadingDegrees(previousHeadingDegrees);
        vesselTransform.rotation = SlerpRotation(
            previousRotation,
            targetRotation,
            interpolationT
        );
    }

    static void CreateVectorLine(GameObject parentObject, string lineName, Color color)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(parentObject.transform);

        if (FlightRenderLayers.HasDedicatedLayers())
        {
            lineObject.layer = FlightRenderLayers.ResolveLocalPhysicsLayer();
        }

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.04f;
        lineRenderer.endWidth = 0.04f;
        Material lineMaterial = RuntimeMaterialFactory.CreateLineMaterial();

        if (lineMaterial != null)
        {
            lineRenderer.material = lineMaterial;
        }
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

    static void CreateEngineExhaust(GameObject shipObject)
    {
        GameObject exhaustObject = new GameObject("EngineExhaust");
        exhaustObject.transform.SetParent(shipObject.transform);
        exhaustObject.transform.localPosition = new Vector3(0.0f, 0.0f, -0.65f);
        exhaustObject.transform.localRotation = EngineExhaustLocalRotation;
        exhaustObject.transform.localScale = Vector3.one;

        ParticleSystem particles = exhaustObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(
            0.05f * EngineExhaustVisualScale,
            0.16f * EngineExhaustVisualScale
        );
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1.0f, 0.45f, 0.08f, 0.95f),
            new Color(1.0f, 0.95f, 0.35f, 0.75f)
        );
        main.maxParticles = 260;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        emission.rateOverTime = 0.0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 13.0f;
        shape.radius = 0.04f * EngineExhaustVisualScale;
        shape.length = 0.16f * EngineExhaustVisualScale;

        ParticleSystemRenderer particleRenderer =
            particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.material = CreateParticleMaterial();
    }

    public static void ClearBlockVisuals(GameObject shipObject)
    {
        if (shipObject == null)
        {
            return;
        }

        Transform blocksContainer = shipObject.transform.Find(BlocksContainerName);

        if (blocksContainer == null)
        {
            return;
        }

        for (int i = blocksContainer.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(blocksContainer.GetChild(i).gameObject);
        }
    }

    static void PruneStaleBlockVisuals(GameObject shipObject, ShipBlockView[] blocks)
    {
        if (shipObject == null)
        {
            return;
        }

        Transform blocksContainer = shipObject.transform.Find(BlocksContainerName);

        if (blocksContainer == null)
        {
            return;
        }

        HashSet<string> expectedFlatBlockNames = BuildExpectedFlatBlockNames(blocks);
        Transform partTree = blocksContainer.Find(FlightVesselSpawner.PartTreeName);

        for (int i = blocksContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = blocksContainer.GetChild(i);

            if (child == null || child == partTree)
            {
                continue;
            }

            if (
                child.name.StartsWith("Block_") &&
                !expectedFlatBlockNames.Contains(child.name)
            )
            {
                Object.Destroy(child.gameObject);
            }
        }
    }

    static HashSet<string> BuildExpectedFlatBlockNames(ShipBlockView[] blocks)
    {
        HashSet<string> names = new HashSet<string>();

        if (blocks == null)
        {
            return names;
        }

        for (int i = 0; i < blocks.Length; i++)
        {
            if (
                blocks[i].hasVabAttach &&
                !string.IsNullOrWhiteSpace(blocks[i].partTemplateId)
            )
            {
                continue;
            }

            if (blocks[i].blockType == ShipBlockType.RcsThruster)
            {
                continue;
            }

            names.Add(BlockMeshFactory.GetBlockObjectName(blocks[i]));
        }

        return names;
    }

    public static void ProcessDestroyedBlocks(
        GameObject shipObject,
        int[] destroyedBlockIds,
        float effectScale
    )
    {
        if (
            shipObject == null ||
            destroyedBlockIds == null ||
            destroyedBlockIds.Length == 0
        )
        {
            return;
        }

        Transform blocksContainer = shipObject.transform.Find(BlocksContainerName);

        if (blocksContainer == null)
        {
            return;
        }

        for (int i = 0; i < destroyedBlockIds.Length; i++)
        {
            int blockId = destroyedBlockIds[i];
            Transform blockTransform = FindBlockVisualByBlockId(blocksContainer, blockId);

            if (blockTransform == null)
            {
                continue;
            }

            PlayBlockDestructionBurst(blockTransform, effectScale);
            Object.Destroy(blockTransform.gameObject);
        }
    }

    static Transform FindBlockVisualByBlockId(Transform root, int blockId)
    {
        if (root == null || blockId <= 0)
        {
            return null;
        }

        string blockName = "Block_" + blockId;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (candidate != null && candidate.name == blockName)
            {
                return candidate;
            }
        }

        return null;
    }

    static void PlayBlockDestructionBurst(Transform blockTransform, float effectScale)
    {
        if (blockTransform == null)
        {
            return;
        }

        float sizeScale = Mathf.Max(
            0.35f,
            blockTransform.lossyScale.magnitude * 0.45f
        );
        ExplosionEffects.PlayBlockImpact(
            blockTransform.position,
            sizeScale,
            effectScale
        );
    }

    public static void Update(
        GameObject bodyObject,
        BodyState bodyState,
        float positionScale,
        global::SimVector3 renderOrigin,
        global::SimVector3? visualAnchorPosition = null
    )
    {
        global::SimVector3 anchorPosition =
            visualAnchorPosition ?? bodyState.position;

        if (WorldVisualBridge.IsCelestialBodyName(bodyState.name))
        {
            UpdateCelestialBody(
                bodyObject,
                bodyState,
                anchorPosition,
                renderOrigin,
                true,
                positionScale <= WorldVisualBridge.MapPositionScale + 1e-6f
            );
            return;
        }

        bool useMeshFramePosition = positionScale <= WorldVisualBridge.MapPositionScale + 1e-6f;

        UpdateVesselBody(
            bodyObject,
            bodyState,
            anchorPosition,
            renderOrigin,
            positionScale,
            useMeshFramePosition
        );
    }

    public static void UpdateCelestialBody(
        GameObject bodyObject,
        BodyState bodyState,
        global::SimVector3 simWorldPosition,
        global::SimVector3 renderOrigin,
        bool visible,
        bool isMapMode,
        double altitudeAbovePlanetMeters = 0.0,
        Simulation simulation = null
    )
    {
        if (bodyObject == null)
        {
            return;
        }

        if (isMapMode)
        {
            ApplyMapModeCelestialBody(
                bodyObject,
                bodyState,
                simWorldPosition,
                renderOrigin,
                visible,
                simulation
            );
        }
        else
        {
            ApplyFlightModeCelestialBody(
                bodyObject,
                bodyState,
                simWorldPosition,
                renderOrigin,
                visible,
                altitudeAbovePlanetMeters,
                simulation
            );
        }

        ApplyCelestialBodyColor(bodyObject, bodyState);
    }

    static void ApplyFlightModeCelestialBody(
        GameObject bodyObject,
        BodyState bodyState,
        global::SimVector3 planetCenterWorld,
        global::SimVector3 shipCenterWorld,
        bool visible,
        double altitudeAbovePlanetMeters,
        Simulation simulation
    )
    {
        if (!visible)
        {
            bodyObject.SetActive(false);
            return;
        }

        bool isLodTerrain = bodyState.name == CelestialTerrainRuntime.LodBodyName &&
            bodyObject.GetComponent<FlightPlanetSphere>() != null;

        if (isLodTerrain)
        {
            bodyObject.SetActive(true);
            ResetCelestialParentScale(bodyObject);
            bodyObject.transform.localScale = Vector3.one;

            if (IsFlightUniverseMovementActive())
            {
                return;
            }

            Vector3 shipRadial = ResolveUnitySurfaceNormal(
                planetCenterWorld,
                shipCenterWorld
            );
            Vector3 lodLocal = WorldVisualBridge.GetFlightPlanetFixedLocalPosition(
                WorldVisualBridge.GetPlanetSurfaceHeightOffsetMeters(simulation)
            );
            bodyObject.transform.localPosition = IsFiniteVector(lodLocal) ?
                lodLocal :
                WorldVisualBridge.GetFlightPlanetFixedLocalPosition(0.0);

            global::SimQuaternion lodSpin = ResolveLodBodySpin(simulation);
            Quaternion flightRotation = WorldVisualBridge.GetFlightPlanetLocalRotation(
                shipRadial,
                lodSpin
            );
            bodyObject.transform.localRotation = QuaternionIsFinite(flightRotation) ?
                flightRotation :
                Quaternion.identity;
            return;
        }

        ApplyFlightModeDistantCelestialBody(
            bodyObject,
            bodyState,
            planetCenterWorld,
            shipCenterWorld,
            simulation
        );
    }

    static global::SimQuaternion ResolveLodBodySpin(Simulation simulation)
    {
        if (simulation == null)
        {
            return global::SimQuaternion.Identity();
        }

        Body lodBody = simulation.GetVisualTerrainBody();

        if (lodBody == null)
        {
            return global::SimQuaternion.Identity();
        }

        return lodBody.GetSpinRotation();
    }

    static void ApplyFlightModeDistantCelestialBody(
        GameObject bodyObject,
        BodyState bodyState,
        global::SimVector3 bodyCenterWorld,
        global::SimVector3 shipCenterWorld,
        Simulation simulation
    )
    {
        bodyObject.SetActive(true);
        ResetCelestialParentScale(bodyObject);

        global::SimVector3 relative = bodyCenterWorld.Subtract(shipCenterWorld);
        if (!WorldVisualBridge.TryGetFlightVisualInertialToFlight(
            out Quaternion inertialToFlight
        ))
        {
            global::SimQuaternion lodSpin = ResolveLodBodySpin(simulation);
            Quaternion lodFlight = WorldVisualBridge.GetFlightPlanetRotationFromBodyFixed(
                WorldVisualBridge.ResolveBodyFixedDirectionUnderShip(simulation, lodSpin)
            );
            inertialToFlight = WorldVisualBridge.GetInertialMeshToFlightRotation(
                lodFlight,
                lodSpin
            );
        }
        Vector3 flightPosition = WorldVisualBridge.InertialOffsetToFlight(
            relative,
            inertialToFlight
        );

        float visualRadius = GetDistantGlobeRenderedRadius(bodyObject, bodyState.radius);
        float centerDistance = flightPosition.magnitude;
        float atmospherePadding = bodyState.name == CelestialTerrainRuntime.EarthBodyName ?
            PlanetDistantVisualPolicy.DistantLimbHeightMeters :
            0f;
        float distantScale = PlanetDistantVisualPolicy.GetDistantSecondaryScale(
            centerDistance,
            visualRadius,
            pullToNearSky: false,
            atmospherePadding
        );

        if (distantScale < 1f)
        {
            flightPosition *= distantScale;
        }

        Vector3 distantLocal = WorldVisualBridge.GetFlightDistantBodyLocalPosition(
            flightPosition,
            WorldVisualBridge.GetFlightUniverseRootLocalPosition(bodyObject.transform)
        );
        if (!IsFiniteVector(distantLocal))
        {
            return;
        }

        bodyObject.transform.localPosition = distantLocal;
        bodyObject.transform.localScale =
            GetCelestialVisualScale(bodyState.radius, WorldVisualBridge.FlightPositionScale) *
            distantScale;
        bodyObject.GetComponent<DistantCelestialGlobe>()?.ApplyCompositing(mapMode: false);

        if (FlightRenderLayers.HasDedicatedLayers())
        {
            FlightRenderLayers.ApplyLocalPhysicsLayer(bodyObject);
        }

        global::SimQuaternion bodySpin = global::SimQuaternion.Identity();

        if (simulation != null)
        {
            if (bodyState.name == "Planet" && simulation.planet != null)
            {
                bodySpin = simulation.planet.GetSpinRotation();
            }
            else if (bodyState.name == "Moon" && simulation.moon != null)
            {
                bodySpin = simulation.moon.GetSpinRotation();
            }
        }

        Quaternion flightSpin = WorldVisualBridge.GetDistantBodyFlightRotation(
            inertialToFlight,
            bodySpin
        );
        bodyObject.transform.localRotation = QuaternionIsFinite(flightSpin) ?
            flightSpin :
            Quaternion.identity;
    }

    static bool QuaternionIsFinite(Quaternion rotation)
    {
        return
            !float.IsNaN(rotation.x) &&
            !float.IsNaN(rotation.y) &&
            !float.IsNaN(rotation.z) &&
            !float.IsNaN(rotation.w) &&
            !float.IsInfinity(rotation.x) &&
            !float.IsInfinity(rotation.y) &&
            !float.IsInfinity(rotation.z) &&
            !float.IsInfinity(rotation.w);
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

    static void ResetCelestialParentScale(GameObject bodyObject)
    {
        Transform parent = bodyObject != null ? bodyObject.transform.parent : null;

        if (parent != null)
        {
            parent.localScale = Vector3.one;
        }
    }

    static void ApplyMapModeCelestialBody(
        GameObject bodyObject,
        BodyState bodyState,
        global::SimVector3 simWorldPosition,
        global::SimVector3 renderOrigin,
        bool visible,
        Simulation simulation
    )
    {
        if (!visible)
        {
            bodyObject.SetActive(false);
            return;
        }

        bodyObject.SetActive(true);
        global::SimVector3 layoutPosition = WorldVisualBridge.GetMapCelestialLayoutSimPosition(
            bodyState.name,
            simWorldPosition,
            renderOrigin,
            simulation
        );
        bodyObject.transform.position = WorldVisualBridge.SimToUnityMeshFramePosition(
            layoutPosition,
            renderOrigin,
            WorldVisualBridge.MapPositionScale
        );
        bodyObject.transform.localScale = GetMapModeCelestialVisualScale(
            bodyObject,
            bodyState
        );
        bodyObject.transform.localRotation = ResolveMapModeCelestialRotation(
            bodyState,
            simulation
        );
        DistantCelestialGlobe distantGlobe = bodyObject.GetComponent<DistantCelestialGlobe>();
        distantGlobe?.ApplyCompositing(mapMode: true);

        if (distantGlobe != null &&
            FlightRenderLayers.HasDedicatedLayers())
        {
            FlightRenderLayers.ApplyScaledSpaceLayer(bodyObject);
        }
        UpdateMapModeCelestialPickCollider(bodyObject, bodyState, visible);
    }

    static void UpdateMapModeCelestialPickCollider(
        GameObject bodyObject,
        BodyState bodyState,
        bool mapModeActive
    )
    {
        if (bodyObject == null ||
            bodyObject.GetComponent<FlightPlanetSphere>() != null ||
            bodyObject.GetComponent<DistantCelestialGlobe>() != null)
        {
            return;
        }

        Transform pickTransform = bodyObject.transform.Find(MapModePickColliderName);
        GameObject pickObject;

        if (pickTransform == null)
        {
            pickObject = new GameObject(MapModePickColliderName);
            pickObject.transform.SetParent(bodyObject.transform, false);
            pickObject.AddComponent<SphereCollider>();
        }
        else
        {
            pickObject = pickTransform.gameObject;
        }

        pickObject.SetActive(mapModeActive);
        pickObject.transform.localPosition = Vector3.zero;
        pickObject.transform.localRotation = Quaternion.identity;
        pickObject.transform.localScale = Vector3.one;

        SphereCollider sphereCollider = pickObject.GetComponent<SphereCollider>();

        if (sphereCollider != null)
        {
            sphereCollider.isTrigger = false;
            // Icosphere radius is 1; parent scale is visual size. Local 70 engulfed Earth.
            sphereCollider.radius = bodyState.name == "Sun" ? 1.5f : 16f;
        }

        if (mapModeActive && FlightRenderLayers.HasDedicatedLayers())
        {
            FlightRenderLayers.ApplyScaledSpaceLayer(pickObject);
        }
    }

    static Quaternion ResolveMapModeCelestialRotation(
        BodyState bodyState,
        Simulation simulation
    )
    {
        if (simulation == null)
        {
            return Quaternion.identity;
        }

        global::SimQuaternion bodySpin = global::SimQuaternion.Identity();

        if (bodyState.name == "Planet" && simulation.planet != null)
        {
            bodySpin = simulation.planet.GetSpinRotation();
        }
        else if (bodyState.name == "Moon" && simulation.moon != null)
        {
            bodySpin = simulation.moon.GetSpinRotation();
        }
        else
        {
            return Quaternion.identity;
        }

        Quaternion mapSpin = WorldVisualBridge.GetMapPlanetMeshFrameSpinRotation(bodySpin);
        return QuaternionIsFinite(mapSpin) ? mapSpin : Quaternion.identity;
    }

    static Vector3 GetMapModeCelestialVisualScale(
        GameObject bodyObject,
        BodyState bodyState
    )
    {
        // Hibernated LOD sphere stays on the object after SoI swap. The visible
        // mesh is then DistantCelestialGlobe (radius 0.5) and needs diameter scale,
        // not MapPositionScale as if the baked-radius FlightPlanetSphere still drew.
        if (DistantCelestialGlobe.IsAttached(bodyObject))
        {
            Vector3 unitScale = GetCelestialVisualScale(
                bodyState.radius,
                WorldVisualBridge.MapPositionScale
            );

            if (bodyState.name == "Sun")
            {
                unitScale *= WorldVisualBridge.MapSunVisualRadiusScale;
            }

            return unitScale;
        }

        FlightPlanetSphere lodSphere = bodyObject.GetComponent<FlightPlanetSphere>();
        if (lodSphere != null && lodSphere.enabled)
        {
            // Flight mesh bakes the body radius; map mode only compresses coordinates.
            return Vector3.one * WorldVisualBridge.MapPositionScale;
        }

        Vector3 scale = GetCelestialVisualScale(
            bodyState.radius,
            WorldVisualBridge.MapPositionScale
        );

        if (bodyState.name == "Sun")
        {
            scale *= WorldVisualBridge.MapSunVisualRadiusScale;
        }

        return scale;
    }

    static float GetDistantGlobeRenderedRadius(GameObject bodyObject, double simRadius)
    {
        float diameterScale = WorldVisualBridge.GetCelestialMeshDiameter(
            simRadius,
            WorldVisualBridge.FlightPositionScale
        );
        float meshRadius = bodyObject != null &&
            bodyObject.GetComponent<DistantCelestialGlobe>() != null ?
            DistantCelestialGlobe.UnitDiameterMeshRadius :
            1f;
        return Mathf.Max(1f, diameterScale * meshRadius);
    }

    static bool IsFlightUniverseMovementActive()
    {
        return Object.FindFirstObjectByType<FlightUniverseMovement>() != null;
    }

    static Vector3 ResolveUnitySurfaceNormal(
        global::SimVector3 planetCenterWorld,
        global::SimVector3 shipCenterWorld
    )
    {
        global::SimVector3 fromPlanetToShip =
            shipCenterWorld.Subtract(planetCenterWorld);

        if (fromPlanetToShip.LengthSquared() <= 1e-12)
        {
            return WorldVisualBridge.GetLaunchSiteNormalUnity();
        }

        global::SimVector3 normal = fromPlanetToShip.Normalize();
        return new Vector3(
            (float)normal.x,
            (float)normal.y,
            (float)normal.z
        );
    }

    static void ApplyCelestialBodyColor(GameObject bodyObject, BodyState bodyState)
    {
        if (bodyObject.GetComponent<FlightPlanetSphere>() != null)
        {
            return;
        }

        if (bodyObject.GetComponent<DistantCelestialGlobe>() != null)
        {
            return;
        }

        if (bodyState.name == "Planet")
        {
            SetColor(bodyObject, new Color(0.2f, 0.6f, 1.0f));
        }
        else if (bodyState.name == "Moon")
        {
            SetColor(bodyObject, new Color(0.8f, 0.8f, 0.8f));
        }
        else if (bodyState.name == "Sun")
        {
            SetColor(bodyObject, WorldVisualBridge.MapSunDiscColor);
        }
        else if (bodyState.isActivePrimary)
        {
            SetColor(bodyObject, new Color(0.3f, 1.0f, 0.3f));
        }
        else
        {
            SetColor(bodyObject, new Color(1.0f, 0.8f, 0.2f));
        }
    }

    public static void UpdateVesselBody(
        GameObject bodyObject,
        BodyState bodyState,
        global::SimVector3 centerOfMassWorld,
        global::SimVector3 renderOrigin,
        float worldScale,
        bool useMeshFramePosition = false,
        bool showDirectionVectors = true
    )
    {
        if (bodyObject == null)
        {
            return;
        }

        EnsureVesselUnityScale(bodyObject);
        EnforceVesselScaleIsolation(bodyObject);

        bodyObject.transform.position = useMeshFramePosition ?
            WorldVisualBridge.SimToUnityMeshFramePosition(
                centerOfMassWorld,
                renderOrigin,
                worldScale
            ) :
            WorldVisualBridge.SimToUnityPosition(
                centerOfMassWorld,
                renderOrigin,
                worldScale
            );

        if (showDirectionVectors)
        {
            UpdateVelocityVector(bodyObject, bodyState);
        }
        else
        {
            ClearVectorLine(bodyObject, "VelocityVector");
            ClearVectorLine(bodyObject, "ThrustDirection");
            ClearVectorLine(bodyObject, "PlannedDeltaV");
            ClearVectorLine(bodyObject, "TargetDirection");
            ClearVectorLine(bodyObject, "TargetRelativeVelocity");
        }

        if (bodyState.name == "Ship" && showDirectionVectors)
        {
            UpdateShipTrail(bodyObject);
        }
    }

    public static Vector3 GetCelestialVisualScale(double simRadius, float positionScale)
    {
        if (simRadius <= 0.0)
        {
            return Vector3.one;
        }

        float sphereDiameter = WorldVisualBridge.GetCelestialMeshDiameter(
            simRadius,
            positionScale
        );
        return Vector3.one * sphereDiameter;
    }

    public static void UpdateThrustDirection(GameObject shipObject, float shipRotationDegrees)
    {
        LineRenderer lineRenderer = GetVectorLine(shipObject, "ThrustDirection");

        if (!IsVectorLineRenderable(lineRenderer))
        {
            return;
        }

        float rotationRadians = shipRotationDegrees * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(
            Mathf.Cos(rotationRadians),
            Mathf.Sin(rotationRadians),
            0
        );

        Vector3 startPosition = shipObject.transform.position;
        Vector3 endPosition = startPosition + direction * ThrustDirectionLength;
        SetVectorLineEndpoints(lineRenderer, startPosition, endPosition);
    }

    public static void UpdatePlannedDeltaV(
        GameObject shipObject,
        global::SimVector3 plannedDeltaV
    )
    {
        LineRenderer lineRenderer = GetVectorLine(shipObject, "PlannedDeltaV");

        if (!IsVectorLineRenderable(lineRenderer))
        {
            return;
        }

        Vector3 startPosition = shipObject.transform.position;

        if (plannedDeltaV.LengthSquared() == 0)
        {
            SetVectorLineEndpoints(lineRenderer, startPosition, startPosition);
            return;
        }

        Vector3 direction = new Vector3(
            (float)plannedDeltaV.x,
            (float)plannedDeltaV.y,
            (float)plannedDeltaV.z
        );
        Vector3 endPosition =
            startPosition + direction.normalized * Mathf.Max(
                0.4f,
                direction.magnitude * PlannedDeltaVVectorScale
            );

        SetVectorLineEndpoints(lineRenderer, startPosition, endPosition);
    }

    public static void UpdateTargetDirection(
        GameObject shipObject,
        Vector3 targetWorldPosition
    )
    {
        LineRenderer lineRenderer = GetVectorLine(shipObject, "TargetDirection");

        if (!IsVectorLineRenderable(lineRenderer))
        {
            return;
        }

        Vector3 startPosition = shipObject.transform.position;
        Vector3 offset = targetWorldPosition - startPosition;

        if (offset.sqrMagnitude <= 0.000001f)
        {
            SetVectorLineEndpoints(lineRenderer, startPosition, startPosition);
            return;
        }

        if (offset.sqrMagnitude < MinTargetLineLength * MinTargetLineLength)
        {
            targetWorldPosition =
                startPosition + offset.normalized * MinTargetLineLength;
        }

        SetVectorLineEndpoints(lineRenderer, startPosition, targetWorldPosition);
    }

    public static GameObject CreateGhostTarget(
        string bodyName,
        double radius,
        float positionScale
    )
    {
        GameObject ghostObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ghostObject.name = "Ghost " + bodyName;
        ghostObject.transform.localScale = GetVisualScale(radius, positionScale);

        Collider collider = ghostObject.GetComponent<Collider>();

        if (collider != null)
        {
            Object.Destroy(collider);
        }

        SetGhostColor(ghostObject, bodyName);
        return ghostObject;
    }

    public static void UpdateGhostTarget(
        GameObject ghostObject,
        string bodyName,
        Vector3 worldPosition,
        global::SimVector3 bodyVelocity,
        double radius,
        float positionScale
    )
    {
        if (ghostObject == null)
        {
            return;
        }

        ghostObject.transform.position = worldPosition;
        ghostObject.transform.localScale = GetVisualScale(radius, positionScale);

        if (bodyVelocity.LengthSquared() > 0.000001)
        {
            float angleDegrees = Mathf.Atan2(
                (float)bodyVelocity.y,
                (float)bodyVelocity.x
            ) * Mathf.Rad2Deg;
            ghostObject.transform.rotation =
                Quaternion.Euler(0.0f, 0.0f, angleDegrees - 90.0f);
        }

        SetGhostColor(ghostObject, bodyName);
    }

    public static Vector3 GetVisualScale(double radius, float positionScale)
    {
        return GetCelestialVisualScale(radius, positionScale);
    }

    static void SetGhostColor(GameObject bodyObject, string bodyName)
    {
        Color baseColor;

        if (bodyName == "Planet")
        {
            baseColor = new Color(0.2f, 0.6f, 1.0f);
        }
        else if (bodyName == "Moon")
        {
            baseColor = new Color(0.8f, 0.8f, 0.8f);
        }
        else
        {
            baseColor = new Color(1.0f, 0.8f, 0.2f);
        }

        baseColor.a = 0.42f;
        Renderer renderer = bodyObject.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.material.color = baseColor;
        }
    }

    public static void UpdateTargetRelativeVelocity(
        GameObject shipObject,
        global::SimVector3 relativeVelocity
    )
    {
        LineRenderer lineRenderer =
            GetVectorLine(shipObject, "TargetRelativeVelocity");

        if (!IsVectorLineRenderable(lineRenderer))
        {
            return;
        }

        Vector3 startPosition = shipObject.transform.position;

        if (relativeVelocity.LengthSquared() <= 0.000001)
        {
            SetVectorLineEndpoints(lineRenderer, startPosition, startPosition);
            return;
        }

        Vector3 velocity = new Vector3(
            (float)relativeVelocity.x,
            (float)relativeVelocity.y,
            (float)relativeVelocity.z
        );
        Vector3 endPosition =
            startPosition + velocity.normalized * Mathf.Max(
                0.45f,
                velocity.magnitude * RelativeVelocityVectorScale
            );

        SetVectorLineEndpoints(lineRenderer, startPosition, endPosition);
    }

    public static void UpdateEngineExhaust(
        GameObject shipObject,
        float throttle,
        bool engineFiring
    )
    {
        ParticleSystem particles = GetEngineExhaust(shipObject);

        if (particles == null)
        {
            return;
        }

        throttle = Mathf.Clamp01(throttle);
        bool shouldEmit = engineFiring && throttle > 0.001f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = shouldEmit;
        emission.rateOverTime = shouldEmit ?
            Mathf.Lerp(MinEngineParticleRate, MaxEngineParticleRate, throttle) :
            0.0f;

        ParticleSystem.MainModule main = particles.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            Mathf.Lerp(0.7f, 1.6f, throttle),
            Mathf.Lerp(1.4f, 4.2f, throttle)
        );
        float sizeScale = EngineExhaustVisualScale;
        main.startSize = new ParticleSystem.MinMaxCurve(
            Mathf.Lerp(0.035f, 0.07f, throttle) * sizeScale,
            Mathf.Lerp(0.08f, 0.20f, throttle) * sizeScale
        );

        if (shouldEmit && !particles.isPlaying)
        {
            particles.Play();
        }
        else if (!shouldEmit && particles.isPlaying)
        {
            particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    static void ClearVectorLine(GameObject bodyObject, string lineName)
    {
        LineRenderer lineRenderer = GetVectorLine(bodyObject, lineName);

        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
        lineRenderer.gameObject.SetActive(false);
    }

    static void UpdateVelocityVector(
        GameObject shipObject,
        BodyState bodyState
    )
    {
        LineRenderer lineRenderer = GetVectorLine(shipObject, "VelocityVector");

        if (lineRenderer == null)
        {
            return;
        }

        Vector3 startPosition = shipObject.transform.position;
        Vector3 velocity = new Vector3(
            (float)bodyState.velocity.x,
            (float)bodyState.velocity.y,
            (float)bodyState.velocity.z
        );

        if (velocity.sqrMagnitude == 0)
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
            return;
        }

        Vector3 endPosition = startPosition + velocity.normalized * VelocityVectorLength;
        SetVectorLineEndpoints(lineRenderer, startPosition, endPosition);
    }

    static bool IsVectorLineRenderable(LineRenderer lineRenderer)
    {
        return
            lineRenderer != null &&
            lineRenderer.gameObject.activeInHierarchy;
    }

    static void SetVectorLineEndpoints(
        LineRenderer lineRenderer,
        Vector3 startPosition,
        Vector3 endPosition
    )
    {
        lineRenderer.positionCount = 2;
        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, startPosition);
        lineRenderer.SetPosition(1, endPosition);
    }

    static LineRenderer GetVectorLine(GameObject bodyObject, string lineName)
    {
        Transform lineTransform = bodyObject.transform.Find(lineName);

        if (lineTransform == null)
        {
            return null;
        }

        return lineTransform.GetComponent<LineRenderer>();
    }

    static ParticleSystem GetEngineExhaust(GameObject bodyObject)
    {
        Transform exhaustTransform = FindDescendantTransform(
            bodyObject.transform,
            "EngineExhaust"
        );

        if (exhaustTransform == null)
        {
            return null;
        }

        return exhaustTransform.GetComponent<ParticleSystem>();
    }

    static void UpdateShipTrail(GameObject shipObject)
    {
        if (!DevSettings.ShipTrailEnabled)
        {
            return;
        }

        ShipTrailView shipTrailView = shipObject.GetComponent<ShipTrailView>();

        if (shipTrailView == null)
        {
            return;
        }

        shipTrailView.AddPoint(shipObject.transform.position);
    }

    static void SetColor(GameObject bodyObject, Color color)
    {
        Renderer[] renderers = bodyObject.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is LineRenderer)
            {
                continue;
            }

            RuntimeMaterialFactory.ApplyColor(renderers[i], color);
        }
    }

    static Material CreateParticleMaterial()
    {
        return RuntimeMaterialFactory.CreateParticleMaterial(Color.white);
    }
}
