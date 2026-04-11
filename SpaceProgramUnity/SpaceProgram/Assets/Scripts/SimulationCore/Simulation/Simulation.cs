using System;

class Simulation
{
    public Body planet;
    public Body moon;
    public Vessel ship;
    public Body[] bodies;
    public SimulationScenario scenario;

    public Vector2 lastGravityAcceleration;
    public Vector2 lastMoonGravityAcceleration;
    public double circularSpeedAtStart;
    public double moonCircularSpeedAtStart;
    public double moonSphereOfInfluenceRadius;
    public double minShipDistanceToPlanet;
    public double maxShipDistanceToPlanet;
    public double initialShipSpecificEnergy;
    public double initialShipAngularMomentum;
    public string activePrimaryBodyName;

    public Simulation(SimulationScenario scenario)
    {
        this.scenario = scenario;

        planet = new Body(
            "Planet",
            Vector2.Zero(),
            Vector2.Zero(),
            Vector2.Zero(),
            50000,
            true
        );

        double moonOrbitRadius = 230;
        Vector2 moonStartPosition = new Vector2(moonOrbitRadius, 0);
        double moonStartRadius = planet.position.Subtract(moonStartPosition).Length();
        moonCircularSpeedAtStart = Gravity.CalculateCircularOrbitSpeed(
            planet.mu,
            moonStartRadius
        );
        Vector2 moonStartVelocity = new Vector2(0, moonCircularSpeedAtStart);

        if (scenario == SimulationScenario.MoonFlyby)
        {
            // Ставим луну в фазу, удобную для первого учебного пролета корабля.
            double moonStartAngle = -2.17;
            moonStartPosition = new Vector2(
                moonOrbitRadius * Math.Cos(moonStartAngle),
                moonOrbitRadius * Math.Sin(moonStartAngle)
            );

            moonStartVelocity = new Vector2(
                -Math.Sin(moonStartAngle),
                Math.Cos(moonStartAngle)
            ).Multiply(moonCircularSpeedAtStart);
        }

        moon = new Body(
            "Moon",
            moonStartPosition,
            moonStartVelocity,
            Vector2.Zero(),
            7000,
            false
        );
        moonSphereOfInfluenceRadius = CalculateSphereOfInfluenceRadius(
            moonStartRadius,
            moon.mu,
            planet.mu
        );

        Vector2 shipStartPosition = new Vector2(-130, 0);
        double shipStartRadius = planet.position.Subtract(shipStartPosition).Length();
        circularSpeedAtStart = Gravity.CalculateCircularOrbitSpeed(
            planet.mu,
            shipStartRadius
        );
        Vector2 shipStartVelocity = new Vector2(0, circularSpeedAtStart);

        if (scenario == SimulationScenario.MoonFlyby)
        {
            // Эллиптический перелет: перицентр у старта корабля, апоцентр около орбиты луны.
            double transferSemiMajorAxis = (shipStartRadius + moonOrbitRadius) * 0.5;
            double transferSpeedAtPeriapsis = Math.Sqrt(
                planet.mu *
                (
                    (2.0 / shipStartRadius) -
                    (1.0 / transferSemiMajorAxis)
                )
            );

            shipStartVelocity = new Vector2(0, transferSpeedAtPeriapsis);
        }

        ship = new Vessel(
            "Ship",
            shipStartPosition,
            shipStartVelocity
        );

        if (
            scenario == SimulationScenario.PlanetMoonShip ||
            scenario == SimulationScenario.MoonFlyby
        )
        {
            bodies = new Body[]
            {
                planet,
                moon,
                ship
            };
        }
        else
        {
            bodies = new Body[]
            {
                planet,
                ship
            };
        }

        lastGravityAcceleration = Vector2.Zero();
        lastMoonGravityAcceleration = Vector2.Zero();
        minShipDistanceToPlanet = shipStartRadius;
        maxShipDistanceToPlanet = shipStartRadius;
        initialShipSpecificEnergy = GetShipSpecificOrbitalEnergyRelativeToPlanet();
        initialShipAngularMomentum = GetShipAngularMomentumRelativeToPlanet();
        activePrimaryBodyName = GetActivePrimaryBody().name;
    }

    public void SetShipThrust(Vector2 thrust)
    {
        ship.thrust = thrust;
    }

    public void ApplyShipDeltaV(Vector2 deltaV)
    {
        ship.velocity = ship.velocity.Add(deltaV);
    }

    // GetBodyStates = получить снимок состояний тел для внешнего слоя, например будущего Unity-адаптера.
    public BodyState[] GetBodyStates()
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        BodyState[] states = new BodyState[bodies.Length];

        for (int i = 0; i < bodies.Length; i++)
        {
            states[i] = new BodyState(
                bodies[i].name,
                bodies[i].position,
                bodies[i].velocity,
                bodies[i].isStatic,
                bodies[i] == activePrimaryBody
            );
        }

        return states;
    }

    // Update = обновить состояние симуляции на один шаг времени.
    public void Update(double dt)
    {
        // Порядок шага оставляем строго таким:
        // 1. CalculateAccelerations()
        // 2. StepPosition()
        // 3. CalculateAccelerations()
        // 4. StepVelocity()
        Vector2[] oldAccelerations = CalculateAccelerations();

        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].acceleration = oldAccelerations[i];
            Integrator.StepPosition(bodies[i], oldAccelerations[i], dt);
        }

        Vector2[] newAccelerations = CalculateAccelerations();

        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].acceleration = newAccelerations[i];

            Integrator.StepVelocity(
                bodies[i],
                oldAccelerations[i],
                newAccelerations[i],
                dt
            );
        }

        // Thrust пока применяем прямо к скорости корабля, без усложнения модели.
        ship.velocity = ship.velocity.Add(ship.thrust.Multiply(dt));

        if (
            scenario == SimulationScenario.PlanetMoonShip ||
            scenario == SimulationScenario.MoonFlyby
        )
        {
            lastMoonGravityAcceleration = newAccelerations[1];
            lastGravityAcceleration = newAccelerations[2];
        }
        else
        {
            lastMoonGravityAcceleration = Vector2.Zero();
            lastGravityAcceleration = newAccelerations[1];
        }

        activePrimaryBodyName = GetActivePrimaryBody().name;
        UpdateOrbitStats();
    }

    // CalculateAccelerations = вычислить ускорения для всех тел.
    Vector2[] CalculateAccelerations()
    {
        Vector2[] accelerations = new Vector2[bodies.Length];

        for (int i = 0; i < bodies.Length; i++)
        {
            Body body = bodies[i];
            Vector2 totalAcceleration = Vector2.Zero();

            if (!body.isStatic)
            {
                for (int j = 0; j < bodies.Length; j++)
                {
                    Body source = bodies[j];

                    if (source == body)
                    {
                        continue;
                    }

                    Vector2 acceleration = Gravity.CalculateAcceleration(
                        body.position,
                        source.position,
                        source.mu
                    );

                    totalAcceleration = totalAcceleration.Add(acceleration);
                }
            }

            accelerations[i] = totalAcceleration;
        }

        return accelerations;
    }

    // UpdateOrbitStats = обновить статистику орбиты корабля.
    void UpdateOrbitStats()
    {
        double distance = GetShipDistanceToPlanet();

        if (distance < minShipDistanceToPlanet)
        {
            minShipDistanceToPlanet = distance;
        }

        if (distance > maxShipDistanceToPlanet)
        {
            maxShipDistanceToPlanet = distance;
        }
    }

    // CalculateSphereOfInfluenceRadius = вычислить радиус сферы влияния меньшего тела вокруг большего.
    double CalculateSphereOfInfluenceRadius(
        double orbitalRadiusAroundPrimary,
        double secondaryMu,
        double primaryMu
    )
    {
        if (primaryMu == 0)
        {
            return 0;
        }

        return orbitalRadiusAroundPrimary * Math.Pow(secondaryMu / primaryMu, 0.4);
    }

    // GetActivePrimaryBody = получить текущее главное тело для орбитальной аналитики.
    Body GetActivePrimaryBody()
    {
        if (
            scenario == SimulationScenario.PlanetMoonShip ||
            scenario == SimulationScenario.MoonFlyby
        )
        {
            double distanceToMoon = GetShipDistanceToMoon();

            if (distanceToMoon <= moonSphereOfInfluenceRadius)
            {
                return moon;
            }
        }

        return planet;
    }

    // GetShipDistanceToActivePrimary = получить расстояние от корабля до текущего главного тела.
    public double GetShipDistanceToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        return activePrimaryBody.position.Subtract(ship.position).Length();
    }

    // GetActivePrimarySphereOfInfluenceRadius = получить радиус текущей сферы влияния для активного тела.
    public double GetActivePrimarySphereOfInfluenceRadius()
    {
        Body activePrimaryBody = GetActivePrimaryBody();

        if (activePrimaryBody == moon)
        {
            return moonSphereOfInfluenceRadius;
        }

        return double.PositiveInfinity;
    }

    // GetShipSpeed = получить скорость корабля.
    public double GetShipSpeed()
    {
        return ship.velocity.Length();
    }

    // GetMoonSpeed = получить скорость луны.
    public double GetMoonSpeed()
    {
        if (
            scenario != SimulationScenario.PlanetMoonShip &&
            scenario != SimulationScenario.MoonFlyby
        )
        {
            return 0;
        }

        return moon.velocity.Length();
    }

    // GetShipDistanceToPlanet = получить расстояние от корабля до планеты.
    public double GetShipDistanceToPlanet()
    {
        return planet.position.Subtract(ship.position).Length();
    }

    // GetShipDistanceToMoon = получить расстояние от корабля до луны.
    public double GetShipDistanceToMoon()
    {
        if (
            scenario != SimulationScenario.PlanetMoonShip &&
            scenario != SimulationScenario.MoonFlyby
        )
        {
            return 0;
        }

        return moon.position.Subtract(ship.position).Length();
    }

    // GetMoonDistanceToPlanet = получить расстояние от луны до планеты.
    public double GetMoonDistanceToPlanet()
    {
        if (
            scenario != SimulationScenario.PlanetMoonShip &&
            scenario != SimulationScenario.MoonFlyby
        )
        {
            return 0;
        }

        return planet.position.Subtract(moon.position).Length();
    }

    // GetShipSpecificOrbitalEnergyRelativeToPlanet = получить удельную орбитальную энергию корабля относительно планеты.
    public double GetShipSpecificOrbitalEnergyRelativeToPlanet()
    {
        return GetShipSpecificOrbitalEnergyRelativeToBody(planet);
    }

    // GetShipAngularMomentumRelativeToPlanet = получить угловой момент корабля относительно планеты.
    public double GetShipAngularMomentumRelativeToPlanet()
    {
        return GetShipAngularMomentumRelativeToBody(planet);
    }

    // GetShipOrbitalEccentricityRelativeToPlanet = получить эксцентриситет орбиты корабля относительно планеты.
    public double GetShipOrbitalEccentricityRelativeToPlanet()
    {
        return GetShipOrbitalEccentricityRelativeToBody(planet);
    }

    // GetShipSpecificOrbitalEnergyRelativeToActivePrimary = получить удельную орбитальную энергию корабля относительно текущего главного тела.
    public double GetShipSpecificOrbitalEnergyRelativeToActivePrimary()
    {
        return GetShipSpecificOrbitalEnergyRelativeToBody(GetActivePrimaryBody());
    }

    // GetShipOrbitTypeRelativeToPlanet = получить тип орбиты корабля относительно планеты.
    public string GetShipOrbitTypeRelativeToPlanet()
    {
        return GetShipOrbitTypeRelativeToBody(planet);
    }

    // GetShipOrbitTypeRelativeToActivePrimary = получить тип орбиты корабля относительно текущего главного тела.
    public string GetShipOrbitTypeRelativeToActivePrimary()
    {
        return GetShipOrbitTypeRelativeToBody(GetActivePrimaryBody());
    }

    // GetShipEscapeSpeedRelativeToPlanet = получить вторую космическую скорость относительно планеты.
    public double GetShipEscapeSpeedRelativeToPlanet()
    {
        return GetShipEscapeSpeedRelativeToBody(planet);
    }

    // GetShipEscapeSpeedRelativeToActivePrimary = получить вторую космическую скорость относительно текущего главного тела.
    public double GetShipEscapeSpeedRelativeToActivePrimary()
    {
        return GetShipEscapeSpeedRelativeToBody(GetActivePrimaryBody());
    }

    // GetShipProgradeDirectionRelativeToActivePrimary = получить направление по скорости относительно текущего главного тела.
    public Vector2 GetShipProgradeDirectionRelativeToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        return ship.velocity.Subtract(activePrimaryBody.velocity).Normalize();
    }

    // GetShipRetrogradeDirectionRelativeToActivePrimary = получить направление против скорости относительно текущего главного тела.
    public Vector2 GetShipRetrogradeDirectionRelativeToActivePrimary()
    {
        return GetShipProgradeDirectionRelativeToActivePrimary().Multiply(-1);
    }

    // GetShipRadialOutDirectionRelativeToActivePrimary = получить направление от текущего главного тела наружу.
    public Vector2 GetShipRadialOutDirectionRelativeToActivePrimary()
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        return ship.position.Subtract(activePrimaryBody.position).Normalize();
    }

    // GetShipRadialInDirectionRelativeToActivePrimary = получить направление к текущему главному телу.
    public Vector2 GetShipRadialInDirectionRelativeToActivePrimary()
    {
        return GetShipRadialOutDirectionRelativeToActivePrimary().Multiply(-1);
    }

    // IsShipBoundToPlanet = проверить, связана ли орбита корабля с планетой.
    public bool IsShipBoundToPlanet()
    {
        return GetShipSpecificOrbitalEnergyRelativeToPlanet() < 0;
    }

    // IsShipBoundToActivePrimary = проверить, связана ли орбита корабля с текущим главным телом.
    public bool IsShipBoundToActivePrimary()
    {
        return GetShipSpecificOrbitalEnergyRelativeToActivePrimary() < 0;
    }

    // GetShipAngularMomentumRelativeToActivePrimary = получить угловой момент корабля относительно текущего главного тела.
    public double GetShipAngularMomentumRelativeToActivePrimary()
    {
        return GetShipAngularMomentumRelativeToBody(GetActivePrimaryBody());
    }

    // GetShipOrbitalEccentricityRelativeToActivePrimary = получить эксцентриситет орбиты корабля относительно текущего главного тела.
    public double GetShipOrbitalEccentricityRelativeToActivePrimary()
    {
        return GetShipOrbitalEccentricityRelativeToBody(GetActivePrimaryBody());
    }

    double GetShipOrbitalEccentricityRelativeToBody(Body body)
    {
        return GetShipOrbitalEccentricityRelativeToBody(body, ship.velocity);
    }

    double GetShipOrbitalEccentricityRelativeToBody(Body body, Vector2 shipVelocity)
    {
        double specificEnergy = GetShipSpecificOrbitalEnergyRelativeToBody(body, shipVelocity);
        double angularMomentum = GetShipAngularMomentumRelativeToBody(body, shipVelocity);
        double eccentricitySquared =
            1.0 +
            (
                2.0 *
                specificEnergy *
                angularMomentum *
                angularMomentum
            ) /
            (body.mu * body.mu);

        if (eccentricitySquared < 0)
        {
            eccentricitySquared = 0;
        }

        return Math.Sqrt(eccentricitySquared);
    }

    // GetShipSemiMajorAxisRelativeToPlanet = получить большую полуось орбиты корабля относительно планеты.
    public double GetShipSemiMajorAxisRelativeToPlanet()
    {
        return GetShipSemiMajorAxisRelativeToBody(planet);
    }

    // GetShipSemiMajorAxisRelativeToActivePrimary = получить большую полуось орбиты корабля относительно текущего главного тела.
    public double GetShipSemiMajorAxisRelativeToActivePrimary()
    {
        return GetShipSemiMajorAxisRelativeToBody(GetActivePrimaryBody());
    }

    double GetShipSemiMajorAxisRelativeToBody(Body body)
    {
        return GetShipSemiMajorAxisRelativeToBody(body, ship.velocity);
    }

    double GetShipSemiMajorAxisRelativeToBody(Body body, Vector2 shipVelocity)
    {
        double specificEnergy = GetShipSpecificOrbitalEnergyRelativeToBody(body, shipVelocity);

        if (specificEnergy == 0)
        {
            return double.PositiveInfinity;
        }

        return -body.mu / (2.0 * specificEnergy);
    }

    // GetShipPeriapsisRelativeToPlanet = получить перицентр орбиты корабля относительно планеты.
    public double GetShipPeriapsisRelativeToPlanet()
    {
        return GetShipPeriapsisRelativeToBody(planet);
    }

    // GetShipPeriapsisRelativeToActivePrimary = получить перицентр орбиты корабля относительно текущего главного тела.
    public double GetShipPeriapsisRelativeToActivePrimary()
    {
        return GetShipPeriapsisRelativeToBody(GetActivePrimaryBody());
    }

    double GetShipPeriapsisRelativeToBody(Body body)
    {
        return GetShipPeriapsisRelativeToBody(body, ship.velocity);
    }

    double GetShipPeriapsisRelativeToBody(Body body, Vector2 shipVelocity)
    {
        double semiMajorAxis = GetShipSemiMajorAxisRelativeToBody(body, shipVelocity);
        double eccentricity = GetShipOrbitalEccentricityRelativeToBody(body, shipVelocity);

        if (double.IsInfinity(semiMajorAxis))
        {
            return double.PositiveInfinity;
        }

        return semiMajorAxis * (1.0 - eccentricity);
    }

    // GetShipApoapsisRelativeToPlanet = получить апоцентр орбиты корабля относительно планеты.
    public double GetShipApoapsisRelativeToPlanet()
    {
        return GetShipApoapsisRelativeToBody(planet);
    }

    // GetShipApoapsisRelativeToActivePrimary = получить апоцентр орбиты корабля относительно текущего главного тела.
    public double GetShipApoapsisRelativeToActivePrimary()
    {
        return GetShipApoapsisRelativeToBody(GetActivePrimaryBody());
    }

    // GetPreviewShipOrbitTypeRelativeToActivePrimary = получить тип орбиты корабля после запланированного delta-v.
    public string GetPreviewShipOrbitTypeRelativeToActivePrimary(Vector2 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        Vector2 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipOrbitTypeRelativeToBody(activePrimaryBody, previewVelocity);
    }

    // GetPreviewShipOrbitalEccentricityRelativeToActivePrimary = получить эксцентриситет орбиты корабля после запланированного delta-v.
    public double GetPreviewShipOrbitalEccentricityRelativeToActivePrimary(Vector2 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        Vector2 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipOrbitalEccentricityRelativeToBody(activePrimaryBody, previewVelocity);
    }

    // GetPreviewShipSemiMajorAxisRelativeToActivePrimary = получить большую полуось орбиты корабля после запланированного delta-v.
    public double GetPreviewShipSemiMajorAxisRelativeToActivePrimary(Vector2 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        Vector2 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipSemiMajorAxisRelativeToBody(activePrimaryBody, previewVelocity);
    }

    // GetPreviewShipPeriapsisRelativeToActivePrimary = получить перицентр орбиты корабля после запланированного delta-v.
    public double GetPreviewShipPeriapsisRelativeToActivePrimary(Vector2 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        Vector2 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipPeriapsisRelativeToBody(activePrimaryBody, previewVelocity);
    }

    // GetPreviewShipApoapsisRelativeToActivePrimary = получить апоцентр орбиты корабля после запланированного delta-v.
    public double GetPreviewShipApoapsisRelativeToActivePrimary(Vector2 plannedDeltaV)
    {
        Body activePrimaryBody = GetActivePrimaryBody();
        Vector2 previewVelocity = ship.velocity.Add(plannedDeltaV);
        return GetShipApoapsisRelativeToBody(activePrimaryBody, previewVelocity);
    }

    double GetShipApoapsisRelativeToBody(Body body)
    {
        return GetShipApoapsisRelativeToBody(body, ship.velocity);
    }

    double GetShipApoapsisRelativeToBody(Body body, Vector2 shipVelocity)
    {
        double semiMajorAxis = GetShipSemiMajorAxisRelativeToBody(body, shipVelocity);
        double eccentricity = GetShipOrbitalEccentricityRelativeToBody(body, shipVelocity);

        if (double.IsInfinity(semiMajorAxis) || eccentricity >= 1.0)
        {
            return double.PositiveInfinity;
        }

        return semiMajorAxis * (1.0 + eccentricity);
    }

    double GetShipSpecificOrbitalEnergyRelativeToBody(Body body)
    {
        return GetShipSpecificOrbitalEnergyRelativeToBody(body, ship.velocity);
    }

    double GetShipSpecificOrbitalEnergyRelativeToBody(Body body, Vector2 shipVelocity)
    {
        Vector2 relativePosition = ship.position.Subtract(body.position);
        Vector2 relativeVelocity = shipVelocity.Subtract(body.velocity);
        double radius = relativePosition.Length();
        double speed = relativeVelocity.Length();

        if (radius == 0)
        {
            return 0;
        }

        double kineticPart = 0.5 * speed * speed;
        double potentialPart = -body.mu / radius;

        return kineticPart + potentialPart;
    }

    string GetShipOrbitTypeRelativeToBody(Body body)
    {
        return GetShipOrbitTypeRelativeToBody(body, ship.velocity);
    }

    string GetShipOrbitTypeRelativeToBody(Body body, Vector2 shipVelocity)
    {
        double specificEnergy = GetShipSpecificOrbitalEnergyRelativeToBody(body, shipVelocity);
        double epsilon = 0.000001;

        if (specificEnergy < -epsilon)
        {
            return "Ellipse";
        }

        if (specificEnergy > epsilon)
        {
            return "Hyperbola";
        }

        return "Parabola";
    }

    double GetShipEscapeSpeedRelativeToBody(Body body)
    {
        double radius = body.position.Subtract(ship.position).Length();

        if (radius == 0)
        {
            return 0;
        }

        return Math.Sqrt(2.0 * body.mu / radius);
    }

    double GetShipAngularMomentumRelativeToBody(Body body)
    {
        return GetShipAngularMomentumRelativeToBody(body, ship.velocity);
    }

    double GetShipAngularMomentumRelativeToBody(Body body, Vector2 shipVelocity)
    {
        Vector2 relativePosition = ship.position.Subtract(body.position);
        Vector2 relativeVelocity = shipVelocity.Subtract(body.velocity);

        return
            relativePosition.x * relativeVelocity.y -
            relativePosition.y * relativeVelocity.x;
    }
}

struct BodyState
{
    public string name;
    public Vector2 position;
    public Vector2 velocity;
    public bool isStatic;
    public bool isActivePrimary;

    public BodyState(
        string name,
        Vector2 position,
        Vector2 velocity,
        bool isStatic,
        bool isActivePrimary
    )
    {
        this.name = name;
        this.position = position;
        this.velocity = velocity;
        this.isStatic = isStatic;
        this.isActivePrimary = isActivePrimary;
    }
}
