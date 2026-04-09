using System;
using System.IO;
using System.Threading;

class Program
{
    const int ScreenWidth = 91;
    const int ScreenHeight = 30;
    const double ScreenScale = 20.0; // Масштаб: сколько единиц мира помещается в одну клетку экрана.

    static void Main()
    {
        RunOrbitTest(SimulationScenario.TwoBodyCircular);
        Console.WriteLine();
        RunOrbitTest(SimulationScenario.PlanetMoonShip);
        // RunVisualization(SimulationScenario.PlanetMoonShip);
    }

    static void RunOrbitTest(SimulationScenario scenario)
    {
        double dt = 0.1;
        int steps = 10000;

        Simulation simulation = new Simulation(scenario);

        for (int step = 0; step < steps; step++)
        {
            simulation.Update(dt);
        }

        double currentEnergy = simulation.GetShipSpecificOrbitalEnergyRelativeToPlanet();
        double currentAngularMomentum = simulation.GetShipAngularMomentumRelativeToPlanet();

        Console.WriteLine("mode = orbit test");
        Console.WriteLine("scenario = " + scenario);
        Console.WriteLine("steps = " + steps);
        Console.WriteLine("dt = " + dt);
        Console.WriteLine("ship speed = " + simulation.GetShipSpeed());
        Console.WriteLine("distance to planet = " + simulation.GetShipDistanceToPlanet());
        Console.WriteLine("min distance to planet = " + simulation.minShipDistanceToPlanet);
        Console.WriteLine("max distance to planet = " + simulation.maxShipDistanceToPlanet);
        Console.WriteLine(
            "distance spread = " +
            (simulation.maxShipDistanceToPlanet - simulation.minShipDistanceToPlanet)
        );
        Console.WriteLine("distance to moon = " + simulation.GetShipDistanceToMoon());
        Console.WriteLine("moon speed = " + simulation.GetMoonSpeed());
        Console.WriteLine("moon distance to planet = " + simulation.GetMoonDistanceToPlanet());
        Console.WriteLine("initial specific energy = " + simulation.initialShipSpecificEnergy);
        Console.WriteLine("current specific energy = " + currentEnergy);
        Console.WriteLine(
            "specific energy delta = " +
            (currentEnergy - simulation.initialShipSpecificEnergy)
        );
        Console.WriteLine("initial angular momentum = " + simulation.initialShipAngularMomentum);
        Console.WriteLine("current angular momentum = " + currentAngularMomentum);
        Console.WriteLine(
            "angular momentum delta = " +
            (currentAngularMomentum - simulation.initialShipAngularMomentum)
        );
    }

    static void RunVisualization(SimulationScenario scenario)
    {
        double dt = 1;

        Simulation simulation = new Simulation(scenario);
        bool[,] trail = new bool[ScreenHeight, ScreenWidth];

        for (int step = 0; step < 120; step++)
        {
            simulation.Update(dt);

            Draw(
                simulation.ship.position,
                simulation.planet.position,
                simulation.moon.position,
                trail
            );

            Console.WriteLine("time = " + ((step + 1) * dt) + " sec");
            Console.WriteLine("scenario = " + scenario);
            Console.WriteLine("speed = " + simulation.GetShipSpeed());
            Console.WriteLine("distance to planet = " + simulation.GetShipDistanceToPlanet());
            Console.WriteLine("distance to moon = " + simulation.GetShipDistanceToMoon());
            Console.WriteLine("min distance to planet = " + simulation.minShipDistanceToPlanet);
            Console.WriteLine("max distance to planet = " + simulation.maxShipDistanceToPlanet);
            Console.WriteLine("gravity acceleration = " + simulation.lastGravityAcceleration.Length());
            Console.WriteLine("circular speed at start = " + simulation.circularSpeedAtStart);
            Console.WriteLine("moon speed = " + simulation.GetMoonSpeed());
            Console.WriteLine("moon distance to planet = " + simulation.GetMoonDistanceToPlanet());
            Thread.Sleep(100);
        }
    }

    static void Draw(
        Vector2 shipPosition,
        Vector2 planetPosition,
        Vector2 moonPosition,
        bool[,] trail
    )
    {
        int centerX = ScreenWidth / 2;
        int centerY = ScreenHeight / 2;

        int planetX = centerX + (int)(planetPosition.x / ScreenScale);
        int planetY = centerY - (int)(planetPosition.y / ScreenScale);

        int moonX = centerX + (int)(moonPosition.x / ScreenScale);
        int moonY = centerY - (int)(moonPosition.y / ScreenScale);

        int shipX = centerX + (int)(shipPosition.x / ScreenScale);
        int shipY = centerY - (int)(shipPosition.y / ScreenScale);

        if (shipX >= 0 && shipX < ScreenWidth && shipY >= 0 && shipY < ScreenHeight)
        {
            trail[shipY, shipX] = true;
        }

        ClearScreen();

        for (int y = 0; y < ScreenHeight; y++)
        {
            for (int x = 0; x < ScreenWidth; x++)
            {
                if (x == planetX && y == planetY)
                {
                    Console.Write("O");
                }
                else if (x == moonX && y == moonY)
                {
                    Console.Write("M");
                }
                else if (x == shipX && y == shipY)
                {
                    Console.Write("*");
                }
                else if (trail[y, x])
                {
                    Console.Write("+");
                }
                else
                {
                    Console.Write(".");
                }
            }

            Console.WriteLine();
        }
    }

    static void ClearScreen()
    {
        try
        {
            Console.Clear();
        }
        catch (IOException)
        {
            Console.WriteLine();
        }
    }
}
