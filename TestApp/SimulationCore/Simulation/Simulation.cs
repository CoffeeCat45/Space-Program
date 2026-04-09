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
    public double minShipDistanceToPlanet;
    public double maxShipDistanceToPlanet;
    public double initialShipSpecificEnergy;
    public double initialShipAngularMomentum;

    public Simulation(SimulationScenario scenario)
    {
        this.scenario = scenario;

        planet = new Body(
            new Vector2(0, 0),
            new Vector2(0, 0),
            50000,
            true
        );

        Vector2 moonStartPosition = new Vector2(230, 0);
        double moonStartRadius = planet.position.Subtract(moonStartPosition).Length();
        moonCircularSpeedAtStart = Gravity.CalculateCircularOrbitSpeed(
            planet.gravityParameter,
            moonStartRadius
        );

        moon = new Body(
            moonStartPosition,
            new Vector2(0, moonCircularSpeedAtStart),
            7000,
            false
        );

        Vector2 shipStartPosition = new Vector2(-130, 0);
        double shipStartRadius = planet.position.Subtract(shipStartPosition).Length();
        circularSpeedAtStart = Gravity.CalculateCircularOrbitSpeed(
            planet.gravityParameter,
            shipStartRadius
        );

        ship = new Vessel(
            shipStartPosition,
            new Vector2(0, circularSpeedAtStart)
        );

        if (scenario == SimulationScenario.PlanetMoonShip)
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

        lastGravityAcceleration = new Vector2(0, 0);
        lastMoonGravityAcceleration = new Vector2(0, 0);
        minShipDistanceToPlanet = shipStartRadius;
        maxShipDistanceToPlanet = shipStartRadius;
        initialShipSpecificEnergy = GetShipSpecificOrbitalEnergyRelativeToPlanet();
        initialShipAngularMomentum = GetShipAngularMomentumRelativeToPlanet();
    }

    public void Update(double dt)
    {
        Vector2[] oldAccelerations = CalculateAccelerations();

        for (int i = 0; i < bodies.Length; i++)
        {
            Integrator.StepPosition(bodies[i], oldAccelerations[i], dt);
        }

        Vector2[] newAccelerations = CalculateAccelerations();

        for (int i = 0; i < bodies.Length; i++)
        {
            Integrator.StepVelocity(
                bodies[i],
                oldAccelerations[i],
                newAccelerations[i],
                dt
            );
        }

        if (scenario == SimulationScenario.PlanetMoonShip)
        {
            lastMoonGravityAcceleration = newAccelerations[1];
            lastGravityAcceleration = newAccelerations[2];
        }
        else
        {
            lastMoonGravityAcceleration = new Vector2(0, 0);
            lastGravityAcceleration = newAccelerations[1];
        }

        UpdateOrbitStats();
    }

    Vector2[] CalculateAccelerations()
    {
        Vector2[] accelerations = new Vector2[bodies.Length];

        for (int i = 0; i < bodies.Length; i++)
        {
            Body body = bodies[i];
            Vector2 totalAcceleration = new Vector2(0, 0);

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
                        source.gravityParameter
                    );

                    totalAcceleration = totalAcceleration.Add(acceleration);
                }
            }

            accelerations[i] = totalAcceleration;
        }

        return accelerations;
    }

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

    public double GetShipSpeed()
    {
        return ship.velocity.Length();
    }

    public double GetMoonSpeed()
    {
        return moon.velocity.Length();
    }

    public double GetShipDistanceToPlanet()
    {
        return planet.position.Subtract(ship.position).Length();
    }

    public double GetShipDistanceToMoon()
    {
        return moon.position.Subtract(ship.position).Length();
    }

    public double GetMoonDistanceToPlanet()
    {
        return planet.position.Subtract(moon.position).Length();
    }

    public double GetShipSpecificOrbitalEnergyRelativeToPlanet()
    {
        Vector2 relativePosition = ship.position.Subtract(planet.position);
        double radius = relativePosition.Length();
        double speed = ship.velocity.Length();

        if (radius == 0)
        {
            return 0;
        }

        double kineticPart = 0.5 * speed * speed;
        double potentialPart = -planet.gravityParameter / radius;

        return kineticPart + potentialPart;
    }

    public double GetShipAngularMomentumRelativeToPlanet()
    {
        Vector2 relativePosition = ship.position.Subtract(planet.position);
        Vector2 relativeVelocity = ship.velocity.Subtract(planet.velocity);

        return
            relativePosition.x * relativeVelocity.y -
            relativePosition.y * relativeVelocity.x;
    }
}
