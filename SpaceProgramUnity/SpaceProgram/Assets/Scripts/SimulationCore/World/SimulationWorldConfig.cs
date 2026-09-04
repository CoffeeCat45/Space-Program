using System;



class SimulationWorldConfig

{

    public const double PlanetMoonMoonSoiMultiplier = 0.75;



    public BodyDefinition[] bodies;

    public ShipStartDefinition shipStart;



    public SimulationWorldConfig(

        BodyDefinition[] bodies,

        ShipStartDefinition shipStart

    )

    {

        this.bodies = bodies;

        this.shipStart = shipStart;

    }



    public static SimulationWorldConfig CreateForScenario(SimulationScenario scenario)

    {

        if (scenario == SimulationScenario.SolarTransfer)

        {

            return CreateSolarSystem();

        }



        if (SimulationScenarios.IsPlanetSurfacePad(scenario))

        {

            return CreatePlanetSurfaceStart();

        }



        if (scenario == SimulationScenario.MoonSurfaceStart)

        {

            return CreateMoonSurfaceStart();

        }



        if (scenario == SimulationScenario.PlanetAtmosphericEntry)

        {

            return CreatePlanetAtmosphericEntry();

        }



        if (scenario == SimulationScenario.InclinedOrbit)

        {

            return CreateInclinedOrbit();

        }



        if (scenario == SimulationScenario.DockingApproach)

        {

            return CreateDockingApproach();

        }



        return CreatePlanetMoonSystem(scenario);

    }



    static SimulationWorldConfig CreatePlanetSurfaceStart()

    {

        BodyDefinition[] bodies =

        {

            CreateSunDefinition(),

            CreateEarthDefinition(

                false,

                CelestialBody.Sun.Name,

                CelestialBody.Sun.PlanetOrbitRadius

            ),

            CreateMoonDefinition(1.15),

        };



        ShipStartDefinition shipStart = ShipStartDefinition.CreateSurfaceStart(

            CelestialBody.Earth.Radius

        );



        return new SimulationWorldConfig(bodies, shipStart);

    }



    static SimulationWorldConfig CreateMoonSurfaceStart()

    {

        BodyDefinition[] bodies =

        {

            CreateSunDefinition(),

            CreateEarthDefinition(

                false,

                CelestialBody.Sun.Name,

                CelestialBody.Sun.PlanetOrbitRadius

            ),

            // Earth at solar phase 0 sits on +X. Moon at Math.PI is on the sunlit side
            // of Earth so the pad is not in Earth umbra at t=0.
            CreateMoonDefinition(Math.PI),

        };


        LaunchSiteConfig.SetActive(LaunchSiteConfig.MoonNearSide);

        ShipStartDefinition shipStart = ShipStartDefinition.CreateSurfaceStart(

            CelestialBody.Moon.Radius

        );

        shipStart.landOnBodyName = CelestialBody.Moon.Name;

        return new SimulationWorldConfig(bodies, shipStart);

    }



    static SimulationWorldConfig CreateDockingApproach()

    {

        BodyDefinition[] bodies = { CreateEarthDefinition(true) };



        ShipStartDefinition shipStart = new ShipStartDefinition(

            SimVector3.Zero(),

            SimVector3.Zero(),

            false

        );



        return new SimulationWorldConfig(bodies, shipStart);

    }



    static SimulationWorldConfig CreateInclinedOrbit()

    {

        BodyDefinition planetDefinition = CreateEarthDefinition(true);

        BodyDefinition[] bodies = { planetDefinition };



        ShipStartDefinition shipStart = ShipStartDefinition.CreateInclinedOrbitStart();



        return new SimulationWorldConfig(bodies, shipStart);

    }



    static SimulationWorldConfig CreatePlanetAtmosphericEntry()

    {

        double moonOrbitPhase = -2.17;



        BodyDefinition[] bodies =

        {

            CreateEarthDefinition(true),

            CreateMoonDefinition(moonOrbitPhase),

        };



        double entryAltitude = 160000.0;

        double shipDistance = CelestialBody.Earth.Radius + entryAltitude;

        double circularSpeed = CelestialBody.Earth.GetCircularOrbitSpeedAtAltitude(

            entryAltitude

        );

        double entrySpeed = circularSpeed * 0.78;



        ShipStartDefinition shipStart = new ShipStartDefinition(

            new SimVector3(-shipDistance, 0, 0),

            new SimVector3(0, entrySpeed, 0),

            false

        );



        return new SimulationWorldConfig(bodies, shipStart);

    }



    static SimulationWorldConfig CreatePlanetMoonSystem(SimulationScenario scenario)

    {

        double moonOrbitPhase = 0;

        double moonOrbitEccentricity = 0;



        if (scenario == SimulationScenario.MoonFlyby)

        {

            moonOrbitPhase = -2.17;

        }



        if (scenario == SimulationScenario.PlanetMoonEllipticMoon)

        {

            moonOrbitEccentricity = 0.12;

        }



        BodyDefinition[] bodies =

        {

            CreateEarthDefinition(true),

            CreateMoonDefinition(moonOrbitPhase, moonOrbitEccentricity),

        };



        ShipStartDefinition shipStart = ShipStartDefinition.CreateLowEarthOrbitStart(

            scenario,

            CelestialBody.Moon.OrbitSemiMajorAxis

        );



        return new SimulationWorldConfig(bodies, shipStart);

    }



    static SimulationWorldConfig CreateSolarSystem()

    {

        BodyDefinition[] bodies =

        {

            CreateSunDefinition(),

            CreateEarthDefinition(false, CelestialBody.Sun.Name, CelestialBody.Sun.PlanetOrbitRadius),

            CreateMoonDefinition(1.4),

        };



        ShipStartDefinition shipStart = ShipStartDefinition.CreateLowEarthOrbitStart(

            SimulationScenario.SolarTransfer,

            CelestialBody.Moon.OrbitSemiMajorAxis

        );



        return new SimulationWorldConfig(bodies, shipStart);

    }



    static BodyDefinition CreateSunDefinition()
    {
        return new BodyDefinition(
            CelestialBody.Sun.Name,
            CelestialBody.Sun.Mu,
            CelestialBody.Sun.Radius,
            true
        );
    }

    static BodyDefinition CreateEarthDefinition(
        bool isStatic,
        string parentName = null,
        double orbitRadius = 0.0
    )
    {
        BodyDefinition definition = new BodyDefinition(
            CelestialBody.Earth.Name,
            CelestialBody.Earth.Mu,
            CelestialBody.Earth.Radius,
            isStatic,
            parentName,
            orbitRadius
        );
        definition.atmosphereProfile = CelestialBody.Earth.CreateAtmosphere();
        return definition;
    }



    static BodyDefinition CreateMoonDefinition(

        double orbitPhaseRadians,

        double orbitEccentricity = 0.0

    )

    {

        return new BodyDefinition(

            CelestialBody.Moon.Name,

            CelestialBody.Moon.Mu,

            CelestialBody.Moon.Radius,

            false,

            CelestialBody.Earth.Name,

            CelestialBody.Moon.OrbitSemiMajorAxis,

            orbitEccentricity,

            orbitPhaseRadians

        );

    }

}



class ShipStartDefinition

{

    public SimVector3 position;

    public SimVector3 velocity;

    public bool relativeToPlanet;

    public bool startLanded;

    public string landOnBodyName;



    public ShipStartDefinition(

        SimVector3 position,

        SimVector3 velocity,

        bool relativeToPlanet,

        bool startLanded = false

    )

    {

        this.position = position;

        this.velocity = velocity;

        this.relativeToPlanet = relativeToPlanet;

        this.startLanded = startLanded;

    }



    public static ShipStartDefinition CreateSurfaceStart(double planetRadius)

    {

        double spawnDistance = LaunchPadConstants.GetPadRestComDistance(
            LandingConfig.ShipCollisionRadius
        );



        return new ShipStartDefinition(

            LandingConfig.GetSurfaceLaunchPadNormal().Multiply(spawnDistance),

            SimVector3.Zero(),

            true,

            true

        );

    }



    public static ShipStartDefinition CreateLowEarthOrbitStart(

        SimulationScenario scenario,

        double moonOrbitRadius

    )

    {

        double orbitRadius = CelestialBody.Earth.GetLowOrbitRadius();

        double circularSpeed = CelestialBody.Earth.GetLowOrbitCircularSpeed();

        SimVector3 shipStartPosition = new SimVector3(-orbitRadius, 0, 0);

        SimVector3 shipStartVelocity = new SimVector3(0, circularSpeed, 0);



        if (scenario == SimulationScenario.MoonFlyby)

        {

            double transferSemiMajorAxis = (orbitRadius + moonOrbitRadius) * 0.5;

            double transferSpeedAtPeriapsis = Math.Sqrt(

                CelestialBody.Earth.Mu *

                (

                    (2.0 / orbitRadius) -

                    (1.0 / transferSemiMajorAxis)

                )

            );



            shipStartVelocity = new SimVector3(0, transferSpeedAtPeriapsis, 0);

        }



        return new ShipStartDefinition(

            shipStartPosition,

            shipStartVelocity,

            false

        );

    }



    public static ShipStartDefinition CreateInclinedOrbitStart()

    {

        double orbitRadius = CelestialBody.Earth.GetLowOrbitRadius();

        double circularSpeed = CelestialBody.Earth.GetLowOrbitCircularSpeed();

        double inclinationRadians =

            LandingConfig.InclinedOrbitTargetInclinationDegrees * Math.PI / 180.0;



        SimVector3 shipStartPosition = new SimVector3(-orbitRadius, 0, 0);

        SimVector3 shipStartVelocity = new SimVector3(

            0,

            circularSpeed * Math.Cos(inclinationRadians),

            circularSpeed * Math.Sin(inclinationRadians)

        );



        return new ShipStartDefinition(

            shipStartPosition,

            shipStartVelocity,

            false,

            false

        );

    }

}

