using System;
using UnityEngine;

/// <summary>
/// KSP-style dual-mode render bridge. SimulationCore uses honest sim-meters only;
/// this type converts sim state to Unity scene coordinates at frame time.
/// </summary>
public static class WorldVisualBridge
{
    /// <summary>Flight mode: 1 sim-meter = 1 Unity meter around the vessel.</summary>
    public const float FlightPositionScale = 1.0f;

    /// <summary>Map mode: compress the solar system for overview (1000×).</summary>
    public const float MapPositionScale = 0.001f;

    /// <summary>
    /// Map heliocentric layout scale about Earth. Must stay 1: the old 4×
    /// Earth-centric offset put the Sun disc away from the physical heliocentric
    /// ship/Earth orbits. Disc size is <see cref="MapSunVisualRadiusScale"/>.
    /// </summary>
    public const double MapHeliocentricVisualScale = 1.0;

    /// <summary>
    /// Toy photosphere (4e6 m) is huge next to Earth. Shrink the map disc;
    /// flight uses a separate camera-facing billboard.
    /// </summary>
    public const float MapSunVisualRadiusScale = 0.4f;

    public static readonly Color MapSunDiscColor = new Color(1.0f, 0.84f, 0.22f);

    /// <summary>Docking test scene: no planet, positions stay 1:1.</summary>
    public const float VacuumDockingPositionScale = 1.0f;

    /// <summary>Flight Scaled Space: honest 1:10 Earth radius in Unity meters.</summary>
    public const float FlightPlanetRadiusUnity = 637100f;

    [System.Obsolete("Use FlightPlanetRadiusUnity for flight-mode planet rendering.")]
    public const float FlightScaledPlanetRadiusUnity = FlightPlanetRadiusUnity;

    /// <summary>Map mode: Earth visual radius in Unity meters (637100 sim-m × 0.001).</summary>
    public const float MapPlanetRadiusUnity = 637.1f;

    /// <summary>
    /// Flight-mode planet center distance from the vessel frame origin (Unity meters).
    /// Vessel stays at origin; planet slides as altitude changes (KSP scaled space).
    /// </summary>
    public static float GetFlightPlanetCenterDistanceUnity(double altitudeAbovePlanetMeters)
    {
        if (double.IsNaN(altitudeAbovePlanetMeters) || double.IsInfinity(altitudeAbovePlanetMeters))
        {
            altitudeAbovePlanetMeters = 0.0;
        }

        return CelestialTerrainRuntime.LodBodyRadiusUnity + (float)Math.Max(0.0, altitudeAbovePlanetMeters);
    }

    /// <summary>
    /// Frame-zero flight up: Unity +Y is zenith; planet center sits on -Y.
    /// </summary>
    public static Vector3 FlightFrameUp => Vector3.up;

    /// <summary>
    /// Slides the universe strictly along -Y as the ship climbs (floating origin).
    /// </summary>
    public static Vector3 GetFlightUniverseRootLocalOffset(double altitudeAbovePlanetMeters)
    {
        if (double.IsNaN(altitudeAbovePlanetMeters) || double.IsInfinity(altitudeAbovePlanetMeters))
        {
            altitudeAbovePlanetMeters = 0.0;
        }

        return FlightFrameUp * -(float)Math.Max(0.0, altitudeAbovePlanetMeters);
    }

    /// <summary>
    /// Universe slide distance when the render origin is the vessel center of mass.
    /// </summary>
    public static Vector3 GetFlightUniverseRootLocalOffsetForShipCom(
        double altitudeAboveSphereMeters,
        double bottomExtentMeters
    )
    {
        if (double.IsNaN(altitudeAboveSphereMeters) || double.IsInfinity(altitudeAboveSphereMeters))
        {
            altitudeAboveSphereMeters = 0.0;
        }

        if (double.IsNaN(bottomExtentMeters) || double.IsInfinity(bottomExtentMeters))
        {
            bottomExtentMeters = 0.0;
        }

        double comAltitudeAboveSphere = Math.Max(
            0.0,
            altitudeAboveSphereMeters + bottomExtentMeters
        );
        return GetFlightUniverseRootLocalOffset(comAltitudeAboveSphere);
    }

    /// <summary>
    /// Planet-center distance to the leveled launch-pad surface in Unity meters.
    /// </summary>
    public static float GetLaunchSiteSurfaceRadiusUnity()
    {
        return CelestialTerrainRuntime.LodBodyRadiusUnity +
            (float)LaunchPadConstants.GetLaunchSiteTerrainHeightAboveSphereMeters();
    }

    /// <summary>Planet-center distance to the launch-pad deck in Unity meters.</summary>
    public static float GetLaunchPadDeckRadiusUnity()
    {
        return CelestialTerrainRuntime.LodBodyRadiusUnity +
            (float)LaunchPadConstants.GetLaunchSitePadDeckHeightAboveSphereMeters();
    }

    /// <summary>
    /// Planet center in the frame-zero flight universe (strictly below the vessel on -Y).
    /// <paramref name="surfaceHeightAboveSphereMeters"/> is the displaced terrain radius
    /// under the viewer (flattened pad base when on the Cape). Without it the mesh sits
    /// surfaceHeight meters above the ship while sim/HUD still report a normal landing.
    /// </summary>
    public static Vector3 GetFlightPlanetFixedLocalPosition(
        double surfaceHeightAboveSphereMeters = 0.0
    )
    {
        if (double.IsNaN(surfaceHeightAboveSphereMeters) ||
            double.IsInfinity(surfaceHeightAboveSphereMeters))
        {
            surfaceHeightAboveSphereMeters = 0.0;
        }

        float radialDistance = CelestialTerrainRuntime.LodBodyRadiusUnity +
            (float)surfaceHeightAboveSphereMeters;
        return FlightFrameUp * -radialDistance;
    }

    /// <summary>
    /// Terrain surface radius at the launch site in Unity meters (flattened pad base).
    /// </summary>
    public static float GetLaunchSiteTerrainSurfaceRadiusUnity()
    {
        return CelestialTerrainRuntime.LodBodyRadiusUnity +
            (float)LaunchPadConstants.GetLaunchSitePadBaseHeightAboveSphereMeters();
    }

    /// <summary>
    /// Unique flight-frame attitude: geographic east → +X, radial up → +Y.
    /// <see cref="Quaternion.FromToRotation"/> only aligns one axis and leaves a free
    /// twist around it — that jitter is the pad shake. Do not add a second heading
    /// patch on top of this; ship and planet must share this helper.
    /// </summary>
    public static Quaternion GetFlightFrameRotationFromEastAndUp(Vector3 eastUnity, Vector3 upUnity)
    {
        if (upUnity.sqrMagnitude <= 1e-8f)
        {
            upUnity = FlightFrameUp;
        }
        else
        {
            upUnity.Normalize();
        }

        Vector3 east = eastUnity - upUnity * Vector3.Dot(eastUnity, upUnity);

        if (east.sqrMagnitude <= 1e-12f)
        {
            east = Vector3.Cross(Vector3.forward, upUnity);

            if (east.sqrMagnitude <= 1e-12f)
            {
                east = Vector3.Cross(Vector3.right, upUnity);
            }
        }

        east.Normalize();
        Vector3 north = Vector3.Cross(east, upUnity);

        if (north.sqrMagnitude <= 1e-12f)
        {
            return Quaternion.FromToRotation(upUnity, FlightFrameUp);
        }

        north.Normalize();
        return Quaternion.Inverse(Quaternion.LookRotation(north, upUnity));
    }

    /// <summary>
    /// Map camera frame with local +Y along inertial radial-out in Mesh Frame.
    /// Not applied as the live ship-follow camera: that stays identity so Game
    /// view matches the vessel / Scene (skybox is the inertial picture).
    /// </summary>
    public static Quaternion GetMapShipOrbitFrameRotation(global::SimVector3 inertialRadialOut)
    {
        if (inertialRadialOut.LengthSquared() <= 1e-12)
        {
            return Quaternion.identity;
        }

        inertialRadialOut = inertialRadialOut.Normalize();
        global::SimVector3 east = LandingConfig.GetSurfaceEastTangent(inertialRadialOut);
        return GetFlightFrameRotationFromEastAndUp(
            SimNormalToMeshFrameUnity(east),
            SimNormalToMeshFrameUnity(inertialRadialOut)
        );
    }

    /// <summary>
    /// Quaternion double-cover: q and −q are the same rotation, but assigning the
    /// flipped sign makes child transforms look like a discrete jump. Keep the
    /// representative closest to the previous frame.
    /// </summary>
    public static Quaternion PickNearestQuaternion(Quaternion previous, Quaternion candidate)
    {
        float dot =
            previous.x * candidate.x +
            previous.y * candidate.y +
            previous.z * candidate.z +
            previous.w * candidate.w;

        if (dot < 0f)
        {
            return new Quaternion(-candidate.x, -candidate.y, -candidate.z, -candidate.w);
        }

        return candidate;
    }

    /// <summary>
    /// 🔒 Flight planet rotation. Visual: ship at origin, planet recedes.
    /// Physics: SoI-centered, rocket moves. Mesh verts are Mesh Frame (x = −sim.x).
    ///
    /// Body-fixed geography under the ship maps to flight +Y; geographic east to +X.
    /// Sidereal spin cancels (surface frame): on the pad the quaternion is constant,
    /// so the pad child does not shake. Same formula after liftoff — no handoff snap.
    /// Canon: context/systems/work_terrain.md §3.1
    /// </summary>
    public static Quaternion GetFlightPlanetLocalRotation(Vector3 shipRadialUnity)
    {
        return GetFlightPlanetLocalRotation(shipRadialUnity, global::SimQuaternion.Identity());
    }

    public static Quaternion GetFlightPlanetLocalRotation(
        Vector3 shipRadialUnity,
        global::SimQuaternion planetSpin
    )
    {
        Vector3 worldRadial = shipRadialUnity.sqrMagnitude > 1e-8f ?
            shipRadialUnity.normalized :
            GetLaunchSiteNormalUnity();
        global::SimVector3 worldRadialSim = new global::SimVector3(
            worldRadial.x,
            worldRadial.y,
            worldRadial.z
        );
        if (worldRadialSim.LengthSquared() > 1e-12)
        {
            worldRadialSim = worldRadialSim.Normalize();
        }

        global::SimVector3 bodyFixedSim = planetSpin.InverseRotate(worldRadialSim);

        if (bodyFixedSim.LengthSquared() <= 1e-12)
        {
            bodyFixedSim = LandingConfig.GetSurfaceLaunchPadNormal();
        }
        else
        {
            bodyFixedSim = bodyFixedSim.Normalize();
        }

        return GetFlightPlanetRotationFromBodyFixed(bodyFixedSim);
    }

    internal static bool IsFlightPadSurfaceLocked(Simulation simulation)
    {
        return
            simulation != null &&
            simulation.ship != null &&
            SimulationScenarios.IsAnySurfacePad(simulation.scenario) &&
            simulation.ship.flightStatus == FlightStatus.Landed &&
            CelestialTerrainRuntime.ApplyLaunchSiteFlatten;
    }

    /// <summary>
    /// Ship-follow Game camera never uses inertial Mesh-to-flight.
    /// Pad, flying, and map stay identity (zenith +Y, same frame as the vessel).
    /// KSP FREE is skybox + no Q/E lock — not rotating the chase camera.
    /// </summary>
    public static bool FlightCameraUsesInertialOrbitFrame(
        bool mapModeActive,
        bool padSurfaceLocked
    )
    {
        if (mapModeActive)
        {
            return false;
        }

        if (padSurfaceLocked)
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Flight universe slide altitude above the reference sphere.
    /// Core pad altitude is already measured from the spawn/deck plane, so it is ~0
    /// while sitting on Cove. The visual pad mesh still sits <c>spawnHeight</c> above
    /// flattened GEBCO. Without adding that height back, Flying looks like the rocket
    /// is inside the deck, then Landed snaps it to SpawnPoint.
    /// </summary>
    internal static double GetFlightVisualAltitudeAboveSphereMeters(Simulation simulation)
    {
        if (simulation == null)
        {
            return 0.0;
        }

        double altitude = simulation.GetShipAltitudeAboveVisualTerrainBody();

        if (UsesPadDeckVisualAltitude(simulation))
        {
            altitude += LaunchPadConstants.GetLaunchSiteSpawnPointHeightAbovePadBaseMeters();
        }

        return altitude;
    }

    static bool UsesPadDeckVisualAltitude(Simulation simulation)
    {
        if (
            simulation.ship == null ||
            !SimulationScenarios.IsAnySurfacePad(simulation.scenario) ||
            !CelestialTerrainRuntime.ApplyLaunchSiteFlatten
        )
        {
            return false;
        }

        return simulation.IsShipOnCovePadContact();
    }

    /// <summary>
    /// Body-fixed ground under the ship, from Core doubles (not Unity floats).
    /// Landed on the pad: launch-site direction, constant every frame.
    /// </summary>
    internal static global::SimVector3 ResolveBodyFixedDirectionUnderShip(
        Simulation simulation,
        global::SimQuaternion planetSpin
    )
    {
        if (IsFlightPadSurfaceLocked(simulation))
        {
            global::SimVector3 landed = simulation.ship.landedSurfaceDirectionBodyFixed;

            if (landed.LengthSquared() > 1e-12)
            {
                return landed.Normalize();
            }

            return LandingConfig.GetSurfaceLaunchPadNormal();
        }

        if (simulation == null || simulation.planet == null || simulation.ship == null)
        {
            return LandingConfig.GetSurfaceLaunchPadNormal();
        }

        global::SimVector3 inertial = simulation.GetShipRadialOutDirectionRelativeToActivePrimary();

        if (inertial.LengthSquared() <= 1e-12)
        {
            Body terrainBody = simulation.GetVisualTerrainBody() ?? simulation.planet;
            inertial = simulation.ship.GetCenterOfMassWorld()
                .Subtract(terrainBody.position);
        }

        if (inertial.LengthSquared() <= 1e-12)
        {
            return LandingConfig.GetSurfaceLaunchPadNormal();
        }

        return planetSpin.InverseRotate(inertial.Normalize()).Normalize();
    }

    public static Quaternion GetFlightPlanetRotationFromBodyFixed(
        global::SimVector3 bodyFixedSim
    )
    {
        if (bodyFixedSim.LengthSquared() <= 1e-12)
        {
            bodyFixedSim = LandingConfig.GetSurfaceLaunchPadNormal();
        }
        else
        {
            bodyFixedSim = bodyFixedSim.Normalize();
        }

        global::SimVector3 bodyEastSim = LandingConfig.GetSurfaceEastTangent(bodyFixedSim);
        return GetFlightFrameRotationFromEastAndUp(
            SimNormalToMeshFrameUnity(bodyEastSim),
            SimNormalToMeshFrameUnity(bodyFixedSim)
        );
    }

    internal static Quaternion GetFlightPlanetLocalRotation(
        Simulation simulation,
        global::SimQuaternion planetSpin
    )
    {
        return GetFlightPlanetRotationFromBodyFixed(
            ResolveBodyFixedDirectionUnderShip(simulation, planetSpin)
        );
    }

    /// <summary>
    /// Same as <see cref="GetFlightPlanetLocalRotation"/> — one visual formula only.
    /// </summary>
    public static Quaternion GetFlightPlanetMeshFrameRotation(
        Vector3 shipRadialUnity,
        global::SimQuaternion planetSpin
    )
    {
        return GetFlightPlanetLocalRotation(shipRadialUnity, planetSpin);
    }

    /// <summary>
    /// Pad placement helper: same unique attitude as flight at the launch site.
    /// </summary>
    public static Quaternion GetFlightPlanetPadLockedMeshFrameRotation()
    {
        return GetFlightPlanetRotationFromBodyFixed(
            LandingConfig.GetSurfaceLaunchPadNormal()
        );
    }

    /// <summary>
    /// Flight picture: ground under the ship. Map uses
    /// <see cref="GetMapPlanetMeshFrameSpinRotation"/> instead.
    /// </summary>
    internal static Quaternion ResolveFlightPlanetVisualRotation(
        Simulation simulation,
        Vector3 shipRadialUnity,
        global::SimQuaternion planetSpin,
        double altitudeAboveSurfaceMeters = 0.0
    )
    {
        if (IsFlightPadSurfaceLocked(simulation))
        {
            return GetFlightPlanetLocalRotation(simulation, planetSpin);
        }

        if (shipRadialUnity.sqrMagnitude > 1e-8f)
        {
            return GetFlightPlanetLocalRotation(shipRadialUnity, planetSpin);
        }

        if (simulation != null)
        {
            return GetFlightPlanetLocalRotation(simulation, planetSpin);
        }

        return GetFlightPlanetLocalRotation(GetLaunchSiteNormalUnity(), planetSpin);
    }

    /// <summary>
    /// Map globe mesh is body-fixed Mesh Frame; ship/orbits are Mesh Frame of inertial sim.
    /// Rotation R satisfies R * FlipX(v) = FlipX(Spin * v) for body-fixed v (R = F Spin F).
    /// Identity spin → identity. Do not offset the ship or special-case a launch site.
    /// </summary>
    public static Quaternion GetMapPlanetMeshFrameSpinRotation(global::SimQuaternion planetSpin)
    {
        Quaternion spinUnity = SimQuaternionToUnityRotation(planetSpin);
        Vector3 up = FlipMeshVertexX(spinUnity * FlipMeshVertexX(Vector3.up));
        Vector3 forward = FlipMeshVertexX(spinUnity * FlipMeshVertexX(Vector3.forward));

        if (up.sqrMagnitude <= 1e-8f)
        {
            up = Vector3.up;
        }
        else
        {
            up.Normalize();
        }

        Vector3 projectedForward = forward - up * Vector3.Dot(forward, up);

        if (projectedForward.sqrMagnitude <= 1e-8f)
        {
            Vector3 right = FlipMeshVertexX(spinUnity * FlipMeshVertexX(Vector3.right));
            projectedForward = Vector3.Cross(right, up);
        }

        if (projectedForward.sqrMagnitude <= 1e-8f)
        {
            return Quaternion.identity;
        }

        return Quaternion.LookRotation(projectedForward.normalized, up);
    }

    /// <summary>
    /// One InverseSpin clock per flight frame: planet mesh, skybox, distant globes.
    /// InverseSpin(now)×lerp(r) is forbidden; callers publish the already-blended
    /// planet localRotation plus interpolated Core spin.
    /// </summary>
    static bool hasFlightVisualInertialFrame;
    static Quaternion flightVisualPlanetLocalRotation = Quaternion.identity;
    static global::SimQuaternion flightVisualLodSpin = global::SimQuaternion.Identity();

    internal static void BeginFlightVisualInertialFrame(
        Quaternion planetFlightLocalRotation,
        global::SimQuaternion interpolatedLodSpin
    )
    {
        flightVisualPlanetLocalRotation = planetFlightLocalRotation;
        flightVisualLodSpin = interpolatedLodSpin;
        hasFlightVisualInertialFrame = true;
    }

    internal static void EndFlightVisualInertialFrame()
    {
        hasFlightVisualInertialFrame = false;
    }

    internal static bool TryGetFlightVisualInertialToFlight(out Quaternion inertialToFlight)
    {
        if (!hasFlightVisualInertialFrame)
        {
            inertialToFlight = Quaternion.identity;
            return false;
        }

        inertialToFlight = GetInertialMeshToFlightRotation(
            flightVisualPlanetLocalRotation,
            flightVisualLodSpin
        );
        return true;
    }

    /// <summary>
    /// Takes inertial Mesh Frame directions into the flight universe (LOD planet
    /// already uses InverseSpin so geography stays under the ship). Distant Earth/Moon
    /// must use this, not <see cref="GetSimToFlightFrameRotation"/> on the inertial
    /// radial — that frame ignores LOD spin and shows the wrong face vs map.
    /// </summary>
    public static Quaternion GetInertialMeshToFlightRotation(
        Quaternion lodFlightRotation,
        global::SimQuaternion lodBodySpin
    )
    {
        Quaternion lodInertial = GetMapPlanetMeshFrameSpinRotation(lodBodySpin);
        Quaternion meshToFlight = lodFlightRotation * Quaternion.Inverse(lodInertial);
        if (
            float.IsNaN(meshToFlight.x) ||
            float.IsNaN(meshToFlight.y) ||
            float.IsNaN(meshToFlight.z) ||
            float.IsNaN(meshToFlight.w)
        )
        {
            return Quaternion.identity;
        }

        float mag = Mathf.Sqrt(
            meshToFlight.x * meshToFlight.x +
            meshToFlight.y * meshToFlight.y +
            meshToFlight.z * meshToFlight.z +
            meshToFlight.w * meshToFlight.w
        );
        if (mag <= 1e-8f)
        {
            return Quaternion.identity;
        }

        return new Quaternion(
            meshToFlight.x / mag,
            meshToFlight.y / mag,
            meshToFlight.z / mag,
            meshToFlight.w / mag
        );
    }

    public static Vector3 InertialOffsetToFlight(
        global::SimVector3 inertialOffset,
        Quaternion inertialMeshToFlight
    )
    {
        return inertialMeshToFlight * SimNormalToMeshFrameUnity(inertialOffset);
    }

    public static Quaternion GetDistantBodyFlightRotation(
        Quaternion inertialMeshToFlight,
        global::SimQuaternion distantBodySpin
    )
    {
        return inertialMeshToFlight * GetMapPlanetMeshFrameSpinRotation(distantBodySpin);
    }

    /// <summary>
    /// Cove pad root on flattened terrain (local meters from planet center).
    /// </summary>
    public static Vector3 GetLaunchPadFixedLocalPositionOnPlanet()
    {
        return GetLaunchSiteNormalMeshFrameUnity() * GetLaunchSiteTerrainSurfaceRadiusUnity();
    }

    /// <summary>
    /// Pad root in the flight-universe frame when planet rotation is locked to +Y.
    /// </summary>
    public static Vector3 GetLaunchPadFixedLocalPositionInFlightUniverse()
    {
        return GetFlightPlanetFixedLocalPosition(
                LaunchPadConstants.GetLaunchSitePadBaseHeightAboveSphereMeters()
            ) +
            FlightFrameUp * GetLaunchSiteTerrainSurfaceRadiusUnity();
    }

    /// <summary>Unity direction from planet center toward the launch site on the pad.</summary>
    public static Vector3 GetFlightPlanetUpNormalUnity()
    {
        return GetLaunchSiteNormalUnity();
    }

    /// <summary>Unity direction from planet center toward the launch site (sim axes).</summary>
    public static Vector3 GetLaunchSiteNormalUnity()
    {
        return SimNormalToUnity(LandingConfig.GetSurfaceLaunchPadNormal());
    }

    /// <summary>
    /// Launch site on the planet mesh (Mesh Frame X-flip). Use for planet rotation and pad placement.
    /// </summary>
    public static Vector3 GetLaunchSiteNormalMeshFrameUnity()
    {
        return SimNormalToMeshFrameUnity(LandingConfig.GetSurfaceLaunchPadNormal());
    }

    /// <summary>
    /// Stable zenith for flight planet rotation on the pad: body-fixed launch normal ×
    /// <b>LOD-body</b> spin (Moon on Moon pad, Earth at the Cape). Using Earth spin here
    /// put the flight sun on the lunar horizon until liftoff.
    /// Flying vessels use live radial from the same visual terrain body.
    /// </summary>
    internal static global::SimVector3 ResolveFlightPlanetUpRadialSim(Simulation simulation)
    {
        if (simulation == null)
        {
            return LandingConfig.GetSurfaceLaunchPadNormal();
        }

        Body lodBody = simulation.GetVisualTerrainBody() ?? simulation.planet;

        if (lodBody == null)
        {
            return LandingConfig.GetSurfaceLaunchPadNormal();
        }

        return ResolveFlightPlanetUpRadialSim(simulation, lodBody.GetSpinRotation());
    }

    internal static global::SimVector3 ResolveFlightPlanetUpRadialSim(
        Simulation simulation,
        global::SimQuaternion planetSpin
    )
    {
        if (simulation == null)
        {
            return LandingConfig.GetSurfaceLaunchPadNormal();
        }

        Body lodBody = simulation.GetVisualTerrainBody() ?? simulation.planet;

        if (
            simulation.ship != null &&
            simulation.ship.flightStatus == FlightStatus.Landed &&
            SimulationScenarios.IsAnySurfacePad(simulation.scenario)
        )
        {
            global::SimVector3 bodyFixed = simulation.ship.landedSurfaceDirectionBodyFixed;

            if (bodyFixed.LengthSquared() <= 1e-12)
            {
                bodyFixed = LandingConfig.GetSurfaceLaunchPadNormal();
            }

            return planetSpin.Rotate(bodyFixed).Normalize();
        }

        if (lodBody != null && simulation.ship != null)
        {
            global::SimVector3 fromLodBody = simulation.ship.GetCenterOfMassWorld()
                .Subtract(lodBody.position);

            if (fromLodBody.LengthSquared() > 1e-12)
            {
                return fromLodBody.Normalize();
            }
        }

        global::SimVector3 shipRadial =
            simulation.GetShipRadialOutDirectionRelativeToActivePrimary();

        if (shipRadial.LengthSquared() > 1e-12)
        {
            return shipRadial.Normalize();
        }

        return LandingConfig.GetSurfaceLaunchPadNormal();
    }

    internal static Vector3 ResolveFlightPlanetUpRadialUnity(Simulation simulation)
    {
        return SimNormalToUnity(ResolveFlightPlanetUpRadialSim(simulation));
    }

    internal static Vector3 ResolveFlightPlanetUpRadialUnity(
        Simulation simulation,
        global::SimQuaternion planetSpin
    )
    {
        return SimNormalToUnity(ResolveFlightPlanetUpRadialSim(simulation, planetSpin));
    }

    /// <summary>Sim unit → geographic face tests (cube-sphere sim canon).</summary>
    public static Vector3 PlanetLocalDirectionToSimUnit(Vector3 planetLocalDirection)
    {
        if (planetLocalDirection.sqrMagnitude <= 1e-8f)
        {
            return GetLaunchSiteNormalUnity();
        }

        return PlanetCubeSphere.MeshFrameUnitToSimUnit(planetLocalDirection.normalized);
    }

    /// <summary>
    /// Sim radial for LOD chunk lookup. Prefer the geography under the vessel in planet-local
    /// mesh space (EarthSandbox / KSP PQS style) so pad-locked planet rotation stays aligned
    /// with quadtree face-UV tests. Falls back to physics sim radial when no transform is set.
    /// </summary>
    /// <summary>World-space anchor for flight LOD (vessel frame zero).</summary>
    public static Vector3 FlightFrameViewerAnchorWorld => Vector3.zero;

    internal static Vector3 ResolveLodFocusDirectionSim(
        Simulation simulation,
        Transform planetTransform = null,
        Vector3 viewerAnchorWorldPosition = default,
        bool hasViewerAnchor = true
    )
    {
        if (hasViewerAnchor && planetTransform != null)
        {
            Vector3 meshLocal = planetTransform.InverseTransformPoint(viewerAnchorWorldPosition);

            if (meshLocal.sqrMagnitude > 1e-6f)
            {
                return PlanetCubeSphere.MeshFrameUnitToSimUnit(meshLocal.normalized);
            }
        }

        if (simulation != null)
        {
            global::SimVector3 shipRadial = ResolveFlightPlanetUpRadialSim(simulation);

            if (shipRadial.LengthSquared() > 1e-12)
            {
                return SimNormalToUnity(shipRadial).normalized;
            }
        }

        return GetLaunchSiteNormalUnity();
    }

    public static Vector3 SimNormalToUnity(global::SimVector3 normal)
    {
        return new Vector3(
            (float)normal.x,
            (float)normal.y,
            (float)normal.z
        );
    }

    /// <summary>Mesh Frame: flip X so Unity markers align with mesh-frame planet geometry.</summary>
    public static Vector3 SimNormalToMeshFrameUnity(global::SimVector3 normal)
    {
        return FlipMeshVertexX(SimNormalToUnity(normal));
    }

    public static Vector3 FlipMeshVertexX(Vector3 value)
    {
        value.x = -value.x;
        return value;
    }

    public static Vector3 SimToUnityMeshFramePosition(
        global::SimVector3 simWorld,
        global::SimVector3 renderOrigin,
        float positionScale
    )
    {
        Vector3 position = SimToUnityPosition(simWorld, renderOrigin, positionScale);
        position.x = -position.x;
        return position;
    }

    internal static bool ShouldExaggerateMapHeliocentric(
        Simulation simulation,
        global::SimVector3 renderOrigin
    )
    {
        _ = simulation;
        _ = renderOrigin;
        return false;
    }

    internal static global::SimVector3 ExaggerateMapAwayFromPlanet(
        global::SimVector3 simWorld,
        Simulation simulation
    )
    {
        if (simulation == null || simulation.planet == null)
        {
            return simWorld;
        }

        global::SimVector3 planet = simulation.planet.position;
        return planet.Add(
            simWorld.Subtract(planet).Multiply(MapHeliocentricVisualScale)
        );
    }

    internal static global::SimVector3 GetMapCelestialLayoutSimPosition(
        string bodyName,
        global::SimVector3 simWorld,
        global::SimVector3 renderOrigin,
        Simulation simulation
    )
    {
        if (bodyName != "Sun")
        {
            return simWorld;
        }

        if (!ShouldExaggerateMapHeliocentric(simulation, renderOrigin))
        {
            return simWorld;
        }

        return ExaggerateMapAwayFromPlanet(simWorld, simulation);
    }

    public static Vector3 SimVelocityToUnityFlight(
        global::SimVector3 velocity,
        Vector3 surfaceNormalUnity
    )
    {
        Vector3 simUnity = SimNormalToUnity(velocity);
        return AlignFlightVectorWithMeshFrameNorth(
            GetSimToFlightFrameRotation(surfaceNormalUnity) * simUnity
        );
    }

    public static Vector3 SimDirectionToFlightWorld(
        global::SimVector3 direction,
        Vector3 surfaceNormalUnity
    )
    {
        return SimVelocityToUnityFlight(direction, surfaceNormalUnity);
    }

    public static Quaternion GetSimToFlightFrameRotation(Vector3 surfaceNormalUnity)
    {
        if (surfaceNormalUnity.sqrMagnitude <= 1e-8f)
        {
            surfaceNormalUnity = GetLaunchSiteNormalUnity();
        }

        // Same unique ENU as the planet (east → +X, radial → +Y). FromTo leaves a free twist.
        Vector3 up = surfaceNormalUnity.normalized;
        Vector3 east = SimNormalToUnity(
            LandingConfig.GetSurfaceEastTangent(
                new global::SimVector3(up.x, up.y, up.z)
            )
        );
        return GetFlightFrameRotationFromEastAndUp(east, up);
    }

    public static Quaternion GetSimToFlightFrameRotation()
    {
        return GetSimToFlightFrameRotation(GetLaunchSiteNormalUnity());
    }

    /// <summary>
    /// Mesh Frame (<c>mesh.x = −sim.x</c>) is a reflection. Planet ENU rebuilds north as
    /// <c>Cross(meshEast, meshUp)</c>, which is opposite <c>flipX(simNorth)</c>. Ground
    /// therefore streams south when the ship flies north if flight vectors stay in sim ENU.
    /// Negate visual Z so ship, camera velocity, and sun match terrain. East (+X) and
    /// zenith (+Y) stay put — do not yaw 180° (that would swap Atlantic/Gulf at Canaveral).
    /// Canon: context/systems/work_terrain.md §3.1
    /// </summary>
    public static Vector3 AlignFlightVectorWithMeshFrameNorth(Vector3 flightEnuVector)
    {
        flightEnuVector.z = -flightEnuVector.z;
        return flightEnuVector;
    }

    public static Quaternion SimOrientationToFlightVisualRotation(
        global::SimQuaternion orientation,
        Vector3 surfaceNormalUnity
    )
    {
        Quaternion enu = GetSimToFlightFrameRotation(surfaceNormalUnity);
        Quaternion body = SimQuaternionToUnityRotation(orientation);
        Vector3 visualUp = AlignFlightVectorWithMeshFrameNorth(enu * (body * Vector3.up));
        Vector3 visualForward = AlignFlightVectorWithMeshFrameNorth(
            enu * (body * Vector3.forward)
        );

        if (visualForward.sqrMagnitude <= 1e-12f)
        {
            visualForward = Vector3.forward;
        }

        if (visualUp.sqrMagnitude <= 1e-12f)
        {
            visualUp = Vector3.up;
        }

        Vector3 reconstructedRight = Vector3.Cross(visualUp, visualForward);

        if (reconstructedRight.sqrMagnitude <= 1e-12f)
        {
            return Quaternion.FromToRotation(Vector3.up, visualUp.normalized);
        }

        return Quaternion.LookRotation(visualForward.normalized, visualUp.normalized);
    }

    public static Quaternion SimOrientationToFlightVisualRotation(global::SimQuaternion orientation)
    {
        return SimOrientationToFlightVisualRotation(
            orientation,
            GetLaunchSiteNormalUnity()
        );
    }

    /// <summary>
    /// Discrete SoI jump only. InverseSpin remaps the new primary radial to +Y, so
    /// the ship visual rotates in world space; the chase camera must take the same
    /// delta or Game view flips. Not a per-frame inertial chase frame.
    /// </summary>
    public static Quaternion GetFlightVisualReorientationDelta(
        global::SimQuaternion shipOrientation,
        global::SimVector3 previousRadialSim,
        global::SimVector3 nextRadialSim
    )
    {
        return GetFlightVisualReorientationDelta(
            shipOrientation,
            SimNormalToUnity(previousRadialSim),
            SimNormalToUnity(nextRadialSim)
        );
    }

    public static Quaternion GetFlightVisualReorientationDelta(
        global::SimQuaternion shipOrientation,
        Vector3 previousSurfaceNormalUnity,
        Vector3 nextSurfaceNormalUnity
    )
    {
        Quaternion previousVisual = SimOrientationToFlightVisualRotation(
            shipOrientation,
            previousSurfaceNormalUnity
        );
        Quaternion nextVisual = SimOrientationToFlightVisualRotation(
            shipOrientation,
            nextSurfaceNormalUnity
        );
        previousVisual = PickNearestQuaternion(nextVisual, previousVisual);
        return nextVisual * Quaternion.Inverse(previousVisual);
    }

    /// <summary>
    /// Identity-frame chase offset: camera at pivot + Euler(pitch, yaw, 0) * back.
    /// </summary>
    public static void FlightOrbitOffsetToYawPitch(
        Vector3 offsetFromPivot,
        out float yawDegrees,
        out float pitchDegrees
    )
    {
        if (offsetFromPivot.sqrMagnitude <= 1e-8f)
        {
            yawDegrees = 0.0f;
            pitchDegrees = 0.0f;
            return;
        }

        Vector3 localOffset = offsetFromPivot.normalized;
        float horizontalDistance = Mathf.Sqrt(
            (localOffset.x * localOffset.x) +
            (localOffset.z * localOffset.z)
        );
        yawDegrees = Mathf.Atan2(localOffset.x, -localOffset.z) * Mathf.Rad2Deg;
        pitchDegrees = Mathf.Atan2(localOffset.y, horizontalDistance) * Mathf.Rad2Deg;
    }

    public static float SignedOrbitRollDegrees(
        Vector3 lookDirection,
        Vector3 frameUp,
        Vector3 cameraUp
    )
    {
        if (lookDirection.sqrMagnitude <= 1e-8f || frameUp.sqrMagnitude <= 1e-8f)
        {
            return 0.0f;
        }

        Quaternion noRoll = Quaternion.LookRotation(
            lookDirection.normalized,
            frameUp.normalized
        );
        Vector3 noRollUp = noRoll * Vector3.up;
        if (cameraUp.sqrMagnitude <= 1e-8f)
        {
            return 0.0f;
        }

        return Vector3.SignedAngle(
            noRollUp,
            cameraUp.normalized,
            lookDirection.normalized
        );
    }

    public static Quaternion SimQuaternionToUnityRotation(global::SimQuaternion orientation)
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

    public static Quaternion GetPadLaunchPlanetRotation()
    {
        return GetFlightPlanetPadLockedMeshFrameRotation();
    }

    /// <summary>
    /// Earth LOD: do not merge the viewer/launch-site subtree below this altitude.
    /// Moon uses <see cref="PlanetLodAltitudePolicy.GetViewerSubtreeProtectAltitudeMeters"/>.
    /// Not a planet-rotation lock.
    /// </summary>
    public const double PadPlanetRotationLockAltitudeMeters =
        PlanetLodAltitudePolicy.EarthViewerSubtreeProtectAltitudeMeters;

    /// <summary>
    /// Mesh height under the ship, so the globe offset matches vertex displacement.
    /// Cove courtyard keeps pad-base flatten. Off the pad this is local GEBCO/LDEM,
    /// not Cape height for the whole flight (that left the rocket hanging in air).
    /// </summary>
    internal static double GetPlanetSurfaceHeightOffsetMeters(Simulation simulation)
    {
        if (simulation == null || simulation.ship == null)
        {
            return 0.0;
        }

        bool padScenario =
            SimulationScenarios.IsAnySurfacePad(simulation.scenario) &&
            CelestialTerrainRuntime.ApplyLaunchSiteFlatten;

        if (padScenario && simulation.IsShipOnCoveLaunchPad())
        {
            return LaunchPadConstants.GetLaunchSitePadBaseHeightAboveSphereMeters();
        }

        Body terrainBody = simulation.GetVisualTerrainBody();

        if (terrainBody == null)
        {
            return 0.0;
        }

        global::SimVector3 radial = simulation.ship.GetCenterOfMassWorld()
            .Subtract(terrainBody.position);

        if (radial.LengthSquared() <= 1e-24)
        {
            return 0.0;
        }

        double heightMeters = PlanetSurfaceContact.GetHeightAboveSphereMeters(
            terrainBody,
            radial
        );
        return heightMeters * PlanetSpherifiedChunkMeshBuilder.ActiveVisualHeightScale;
    }

    public const float FlightCameraNearClip = 1.0f;
    /// <summary>
    /// Toy Earth–Moon (SMA 4.5 Mm) at 1:1. Clip-scale of the Moon into ship space
    /// made the globe closer than physics (fly-through vs map miss). 1:10 SMA 38 Mm
    /// needs a separate scaled-space camera — do not fake the distance here.
    /// </summary>
    public const float FlightCameraFarClip = 8_000_000f;
    public const float MapCameraNearClip = 1.0f;
    public const float MapCameraFarClip = 100_000_000f;

    /// <summary>
    /// Distant Moon/Earth globes live under <see cref="FlightUniverseRoot"/>, which
    /// slides on −Y so the LOD planet sits under the ship. Convert a ship-frame
    /// position into that child's localPosition by undoing the slide. Without this
    /// the Moon is drawn ~altitude closer than physics (FP "on the Moon", map far).
    /// </summary>
    public static Vector3 GetFlightDistantBodyLocalPosition(
        Vector3 positionInShipFrame,
        Vector3 universeRootLocalPosition
    )
    {
        return positionInShipFrame - universeRootLocalPosition;
    }

    public static Vector3 GetFlightUniverseRootLocalPosition(Transform bodyTransform)
    {
        if (bodyTransform == null)
        {
            return Vector3.zero;
        }

        FlightUniverseRoot universeRoot = bodyTransform.GetComponentInParent<FlightUniverseRoot>();

        if (universeRoot == null)
        {
            return Vector3.zero;
        }

        return universeRoot.transform.localPosition;
    }

    /// <summary>
    /// KSP-style distant planet: shrink and pull the globe toward the ship so the far
    /// limb stays inside <see cref="FlightCameraFarClip"/>. Angular size is unchanged.
    /// </summary>
    public static float GetFlightDistantVisualScale(
        double altitudeAbovePlanetMeters,
        double bottomExtentMeters,
        double surfaceHeightAboveSphereMeters
    )
    {
        return PlanetDistantVisualPolicy.GetVisualScale(
            altitudeAbovePlanetMeters,
            bottomExtentMeters,
            surfaceHeightAboveSphereMeters
        );
    }

    public const float NormalCameraOrbitUnityMin = 4.0f;
    public const float NormalCameraOrbitUnityMax = 150.0f;

    /// <summary>
    /// Keep the map camera outside the visual atmosphere shell (R + 115 km, mesh ×1.08).
    /// Closer than that, the limb shader switches to interior sky and the halo vanishes.
    /// </summary>
    public const double MapOrbitSimMetersMin =
        (PlanetSurfaceConstants.UnityFlightPlanetRadiusMeters +
         FlightSkyVisibilitySettings.AtmosphereShellHeightMeters) * 1.12;

    /// <summary>
    /// Ship-focused map: stay outside the vessel, not the whole atmosphere shell
    /// measured from planet center.
    /// </summary>
    public const double MapShipOrbitSimMetersMin = 2_000.0;

    /// <summary>First M / F on the vessel: regional view, not a 45 m chase leftover.</summary>
    public const double MapShipOrbitSimMetersDefault = 800_000.0;

    /// <summary>
    /// Resolved per-frame render policy for the two independent visual worlds.
    /// </summary>
    public readonly struct RenderContext
    {
        public readonly bool IsMapMode;
        public readonly bool VacuumDocking;
        public readonly float PositionScale;
        public readonly bool ShowVesselMeshes;
        public readonly bool ShowCelestialMeshes;

        public RenderContext(bool isMapMode, bool vacuumDocking)
        {
            IsMapMode = isMapMode;
            VacuumDocking = vacuumDocking;

            if (vacuumDocking)
            {
                PositionScale = VacuumDockingPositionScale;
                ShowVesselMeshes = !isMapMode;
                ShowCelestialMeshes = false;
                return;
            }

            PositionScale = isMapMode ? MapPositionScale : FlightPositionScale;
            ShowVesselMeshes = !isMapMode;
            ShowCelestialMeshes = true;
        }
    }

    public static RenderContext Resolve(bool isMapMode, bool vacuumDocking)
    {
        return new RenderContext(isMapMode, vacuumDocking);
    }

    public static float GetPositionScale(bool isMapMode, bool vacuumDocking)
    {
        if (vacuumDocking)
        {
            return VacuumDockingPositionScale;
        }

        return isMapMode ? MapPositionScale : FlightPositionScale;
    }

    public static Vector3 SimToUnityPosition(
        global::SimVector3 simWorld,
        global::SimVector3 renderOrigin,
        float positionScale
    )
    {
        global::SimVector3 local = simWorld.Subtract(renderOrigin);
        return new Vector3(
            (float)(local.x * positionScale),
            (float)(local.y * positionScale),
            (float)(local.z * positionScale)
        );
    }

    public static float GetCelestialMeshDiameter(double simRadius, float positionScale)
    {
        if (simRadius <= 0.0)
        {
            return 1.0f;
        }

        return (float)(simRadius * 2.0 * positionScale);
    }

    public static bool ShouldRenderCelestialBodyInFlight(string bodyName)
    {
        return bodyName == "Planet" || bodyName == "Moon";
    }

    public static bool IsCelestialBodyName(string bodyName)
    {
        return
            bodyName == "Planet" ||
            bodyName == "Moon" ||
            bodyName == "Sun";
    }

    public static bool IsVesselBodyName(string bodyName)
    {
        return bodyName == "Ship" || bodyName == "Station";
    }

    public static bool TryGetMapModePlanetOcclusionSphere(
        Transform planetTransform,
        out Vector3 planetCenter,
        out float planetRadius
    )
    {
        planetCenter = Vector3.zero;
        planetRadius = MapPlanetRadiusUnity;

        if (planetTransform == null)
        {
            return false;
        }

        planetCenter = planetTransform.position;
        planetRadius = CelestialTerrainRuntime.LodBodyRadiusUnity *
            Mathf.Max(planetTransform.lossyScale.x, 1e-6f);
        return true;
    }

    public static bool TryGetCelestialOcclusionSphere(
        Transform bodyTransform,
        out Vector3 center,
        out float radius
    )
    {
        center = Vector3.zero;
        radius = 0f;

        if (bodyTransform == null)
        {
            return false;
        }

        center = bodyTransform.position;
        Transform mapSphere = bodyTransform.Find("PlanetMapSphere");
        // Hibernated LOD leaves an inactive baked-radius child. After SoI swap the
        // parent is a unit-diameter distant globe — that child would read as a
        // multi-million-unit occluder and swallow the ship marker.
        if (
            mapSphere != null &&
            mapSphere.gameObject.activeInHierarchy &&
            TryGetMeshOcclusionSphere(mapSphere, out center, out radius)
        )
        {
            return true;
        }

        if (TryGetMeshOcclusionSphere(bodyTransform, out center, out radius))
        {
            return true;
        }

        return TryGetMapModePlanetOcclusionSphere(bodyTransform, out center, out radius);
    }

    static bool TryGetMeshOcclusionSphere(
        Transform meshTransform,
        out Vector3 center,
        out float radius
    )
    {
        center = Vector3.zero;
        radius = 0f;

        if (meshTransform == null)
        {
            return false;
        }

        MeshFilter filter = meshTransform.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            return false;
        }

        center = meshTransform.position;
        Vector3 extents = filter.sharedMesh.bounds.extents;
        float meshRadius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
        float scale = Mathf.Max(meshTransform.lossyScale.x, 1e-8f);
        radius = meshRadius * scale;
        return radius > 0f;
    }

    public static bool IsMapMarkerVisibleFromCamera(
        Camera camera,
        Vector3 markerWorldPosition,
        Vector3 planetCenter,
        float planetRadius,
        bool hasPlanetSphere,
        Plane[] frustumPlanes = null
    )
    {
        if (camera == null)
        {
            return true;
        }

        Vector3 cameraPosition = camera.transform.position;
        Vector3 cameraToMarker = markerWorldPosition - cameraPosition;

        if (cameraToMarker.sqrMagnitude <= 1e-8f)
        {
            return true;
        }

        if (Vector3.Dot(camera.transform.forward, cameraToMarker) <= 0f)
        {
            return false;
        }

        if (frustumPlanes != null && frustumPlanes.Length == 6)
        {
            Bounds markerBounds = new Bounds(
                markerWorldPosition,
                Vector3.one * 0.05f
            );

            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, markerBounds))
            {
                return false;
            }
        }

        if (!hasPlanetSphere)
        {
            return true;
        }

        Vector3 markerOffset = markerWorldPosition - planetCenter;

        if (markerOffset.sqrMagnitude <= planetRadius * planetRadius)
        {
            return false;
        }

        return !IsWorldPointOccludedBySphere(
            cameraPosition,
            markerWorldPosition,
            planetCenter,
            planetRadius
        );
    }

    public static bool IsWorldPointOccludedBySphere(
        Vector3 rayOrigin,
        Vector3 targetPosition,
        Vector3 sphereCenter,
        float sphereRadius
    )
    {
        Vector3 toTarget = targetPosition - rayOrigin;
        float targetDistance = toTarget.magnitude;

        if (targetDistance <= 1e-5f)
        {
            return false;
        }

        Vector3 rayDirection = toTarget / targetDistance;
        Vector3 originToCenter = rayOrigin - sphereCenter;
        if (originToCenter.sqrMagnitude < sphereRadius * sphereRadius)
        {
            return false;
        }

        float projection = Vector3.Dot(rayDirection, originToCenter);
        float discriminant =
            projection * projection -
            (originToCenter.sqrMagnitude - sphereRadius * sphereRadius);

        if (discriminant < 0f)
        {
            return false;
        }

        float intersectionDistance = -projection - Mathf.Sqrt(discriminant);

        return intersectionDistance > 1e-4f && intersectionDistance < targetDistance;
    }
}
