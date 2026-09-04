using System;

class SimulationWorldSetupResult
{
    public Body sun;
    public Body planet;
    public Body moon;
    public Vessel ship;
    public Vessel dockableStation;
    public Body[] bodies;
    public double moonSphereOfInfluenceRadius;
    public double planetSphereOfInfluenceRadius;
    public double circularSpeedAtStart;
    public double moonCircularSpeedAtStart;
}

static class SimulationWorldBuilder
{
    public static SimulationWorldSetupResult Build(
        SimulationScenario scenario,
        IGravityModel gravityModel
    )
    {
        if (scenario == SimulationScenario.DockingApproach)
        {
            return BuildDockingVacuum();
        }

        SimulationWorldConfig config =
            SimulationWorldConfig.CreateForScenario(scenario);
        SimulationWorldSetupResult result = new SimulationWorldSetupResult();

        BodyDefinition planetDefinition = FindBodyDefinition(config, "Planet");
        BodyDefinition moonDefinition = FindBodyDefinition(config, "Moon");
        BodyDefinition sunDefinition = FindBodyDefinition(config, "Sun");
        bool hasMoon = moonDefinition != null;

        if (sunDefinition != null)
        {
            result.sun = CreateBody(sunDefinition, SimVector3.Zero(), SimVector3.Zero());
            result.sun.isRootBody = CelestialBody.Sun.IsRootBody;
            result.sun.sphereOfInfluenceRadius = 0.0;
        }

        SimVector3 planetPosition = SimVector3.Zero();
        SimVector3 planetVelocity = SimVector3.Zero();

        if (planetDefinition.parentName == "Sun" && result.sun != null)
        {
            InitializeOrbitAroundParent(
                planetDefinition,
                result.sun,
                gravityModel,
                out planetPosition,
                out planetVelocity
            );
        }

        result.planet = CreateBody(
            planetDefinition,
            planetPosition,
            planetVelocity
        );

        SimVector3 moonPosition = SimVector3.Zero();
        SimVector3 moonVelocity = SimVector3.Zero();

        if (hasMoon && moonDefinition != null)
        {
            InitializeOrbitAroundParent(
                moonDefinition,
                result.planet,
                gravityModel,
                out moonPosition,
                out moonVelocity
            );

            result.moon = CreateBody(
                moonDefinition,
                moonPosition,
                moonVelocity
            );

            double moonSoiReferenceRadius = moonDefinition.orbitRadius;

            if (scenario == SimulationScenario.PlanetMoonEllipticMoon)
            {
                moonSoiReferenceRadius = moonDefinition.orbitRadius;
            }

            double moonMuForSoi = result.moon.mu;

            result.moonSphereOfInfluenceRadius = CalculateSphereOfInfluenceRadius(
                moonSoiReferenceRadius,
                moonMuForSoi,
                result.planet.mu
            );

            if (scenario != SimulationScenario.SolarTransfer)
            {
                result.moonSphereOfInfluenceRadius *=
                    SimulationWorldConfig.PlanetMoonMoonSoiMultiplier;
            }

            result.moon.sphereOfInfluenceRadius =
                result.moonSphereOfInfluenceRadius;
        }

        if (result.sun != null)
        {
            result.planetSphereOfInfluenceRadius =
                CelestialBody.Earth.GetSphereOfInfluenceRadius();
            result.planet.sphereOfInfluenceRadius =
                result.planetSphereOfInfluenceRadius;
        }

        SimVector3 shipPosition = config.shipStart.position;
        SimVector3 shipVelocity = config.shipStart.velocity;
        Body landBody = result.planet;

        if (
            hasMoon &&
            result.moon != null &&
            config.shipStart.landOnBodyName == CelestialBody.Moon.Name
        )
        {
            landBody = result.moon;
        }

        if (config.shipStart.relativeToPlanet && landBody != null)
        {
            shipPosition = landBody.position.Add(shipPosition);
            shipVelocity = landBody.velocity.Add(shipVelocity);
        }

        result.ship = new Vessel("Ship", shipPosition, shipVelocity);

        if (config.shipStart.startLanded && landBody != null)
        {
            PlaceShipOnPlanetSurface(result.ship, landBody);
        }

        result.circularSpeedAtStart = gravityModel.CalculateCircularOrbitSpeed(
            result.planet.mu,
            result.planet.position.Subtract(shipPosition).Length()
        );

        if (result.moon != null)
        {
            result.moonCircularSpeedAtStart = gravityModel.CalculateCircularOrbitSpeed(
                result.planet.mu,
                result.planet.position.Subtract(result.moon.position).Length()
            );
        }

        result.bodies = BuildBodiesArray(result, hasMoon);
        return result;
    }

    static SimulationWorldSetupResult BuildDockingVacuum()
    {
        SimulationWorldSetupResult result = new SimulationWorldSetupResult();
        result.planet = null;
        result.moon = null;
        result.sun = null;

        DockingScenarioSetup.ConfigureVacuumApproach(
            out SimVector3 stationPosition,
            out SimVector3 stationVelocity,
            out double stationHeadingRadians,
            out SimVector3 shipPosition,
            out SimVector3 shipVelocity,
            out double shipHeadingRadians
        );

        result.ship = new Vessel("Ship", shipPosition, shipVelocity);
        result.ship.SetHeadingRadians(shipHeadingRadians);
        result.ship.flightStatus = FlightStatus.Flying;

        Vessel station = new Vessel("Station", stationPosition, stationVelocity);
        station.SetHeadingRadians(stationHeadingRadians);
        station.flightStatus = FlightStatus.Flying;
        result.dockableStation = station;

        result.bodies = new Body[] { result.ship };
        result.circularSpeedAtStart = 0.0;
        return result;
    }

    static void PlaceShipOnPlanetSurface(Vessel ship, Body planet)
    {
        SimVector3 outward = LandingConfig.GetSurfaceLaunchPadNormal();
        ship.lastSurfaceNormal = outward;
        double spawnDistance = LaunchPadConstants.GetPadRestComDistance(
            LandingConfig.ShipCollisionRadius
        );
        ship.position = planet.position.Add(
            outward.Multiply(spawnDistance)
        );
        ship.velocity = planet.GetAbsoluteSurfaceVelocityAt(ship.GetCenterOfMassWorld());
        ship.flightStatus = FlightStatus.Landed;
        ship.landedOnBodyName = planet.name;
        ship.landedSurfaceDirectionBodyFixed = outward.Normalize();
    }

    static BodyDefinition FindBodyDefinition(
        SimulationWorldConfig config,
        string name
    )
    {
        for (int i = 0; i < config.bodies.Length; i++)
        {
            if (config.bodies[i].name == name)
            {
                return config.bodies[i];
            }
        }

        return null;
    }

    static Body CreateBody(
        BodyDefinition definition,
        SimVector3 position,
        SimVector3 velocity
    )
    {
        return new Body(
            definition.name,
            position,
            velocity,
            SimVector3.Zero(),
            definition.mu,
            definition.radius,
            definition.isStatic
        )
        {
            atmosphereProfile = definition.atmosphereProfile
        };
    }

    static void InitializeOrbitAroundParent(
        BodyDefinition definition,
        Body parent,
        IGravityModel gravityModel,
        out SimVector3 position,
        out SimVector3 velocity
    )
    {
        double orbitRadius = definition.orbitRadius;

        if (definition.orbitEccentricity > 0)
        {
            orbitRadius = definition.orbitRadius * (1.0 - definition.orbitEccentricity);
        }

        double phase = definition.orbitPhaseRadians;
        SimVector3 localPosition = new SimVector3(
            orbitRadius * Math.Cos(phase),
            orbitRadius * Math.Sin(phase),
            0
        );
        SimVector3 tangent = new SimVector3(
            -Math.Sin(phase),
            Math.Cos(phase),
            0
        );

        double orbitalSpeed;

        if (definition.orbitEccentricity > 0)
        {
            double semiMajorAxis = definition.orbitRadius;
            orbitalSpeed = Math.Sqrt(
                parent.mu *
                (
                    (2.0 / orbitRadius) -
                    (1.0 / semiMajorAxis)
                )
            );
        }
        else
        {
            orbitalSpeed = gravityModel.CalculateCircularOrbitSpeed(
                parent.mu,
                orbitRadius
            );
        }

        position = parent.position.Add(localPosition);
        velocity = parent.velocity.Add(tangent.Multiply(orbitalSpeed));
    }

    static Body[] BuildBodiesArray(
        SimulationWorldSetupResult result,
        bool hasMoon
    )
    {
        if (result.sun != null)
        {
            if (hasMoon)
            {
                return new Body[]
                {
                    result.sun,
                    result.planet,
                    result.moon,
                    result.ship
                };
            }

            return new Body[]
            {
                result.sun,
                result.planet,
                result.ship
            };
        }

        if (hasMoon)
        {
            return new Body[]
            {
                result.planet,
                result.moon,
                result.ship
            };
        }

        return new Body[]
        {
            result.planet,
            result.ship
        };
    }

    static double CalculateSphereOfInfluenceRadius(
        double orbitalRadiusAroundPrimary,
        double secondaryMu,
        double primaryMu
    )
    {
        if (primaryMu == 0)
        {
            return 0;
        }

        return orbitalRadiusAroundPrimary *
            Math.Pow(secondaryMu / primaryMu, 0.4);
    }
}
