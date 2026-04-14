using System;
using System.IO;
using System.Threading;

class Program
{
    const int ScreenWidth = 81;
    const int ScreenHeight = 25;
    const double ScreenScale = 20.0; // Масштаб: сколько единиц мира помещается в одну клетку экрана.
    const double ManualThrustPower = 1.0; // Сила ручного thrust для одного кадра.
    const double ManualImpulseDeltaV = 0.5; // Величина одного импульса delta-v.

    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "stability")
        {
            RunStabilityReport();
            return;
        }

        RunVisualization(SimulationScenario.MoonFlyby);
        // RunOrbitTest(SimulationScenario.TwoBodyCircular);
        // RunOrbitTest(SimulationScenario.PlanetMoonShip);
        // RunOrbitTest(SimulationScenario.MoonFlyby);
    }

    static void PrintInfo(string englishLabel, string russianLabel, object value)
    {
        Console.WriteLine(englishLabel + " (" + russianLabel + "): " + value);
    }

    static void PrintSection(string title)
    {
        Console.WriteLine();
        Console.WriteLine("=== " + title + " ===");
    }

    // RunOrbitTest = запустить длинный тест орбиты без отрисовки.
    static void RunStabilityReport()
    {
        RunStabilityCase("planet circular", CreatePlanetCircularTest(), 0.02, 100000);
        RunStabilityCase("moon circular", CreateMoonCircularTest(), 0.02, 100000);
    }

    static Simulation CreatePlanetCircularTest()
    {
        return new Simulation(SimulationScenario.TwoBodyCircular);
    }

    static Simulation CreateMoonCircularTest()
    {
        Simulation simulation = new Simulation(SimulationScenario.MoonFlyby);
        double orbitRadius = 60.0;
        double circularSpeed = Gravity.CalculateCircularOrbitSpeed(
            simulation.moon.mu,
            orbitRadius
        );

        simulation.ship.position = simulation.moon.position.Add(
            new Vector2(orbitRadius, 0.0)
        );
        simulation.ship.velocity = simulation.moon.velocity.Add(
            new Vector2(0.0, circularSpeed)
        );
        simulation.ship.thrust = Vector2.Zero();
        simulation.ship.thrustAcceleration = Vector2.Zero();
        simulation.activePrimaryBodyName = "Moon";

        return simulation;
    }

    static void RunStabilityCase(
        string label,
        Simulation simulation,
        double dt,
        int steps
    )
    {
        double minDistance = simulation.GetShipDistanceToActivePrimary();
        double maxDistance = minDistance;

        for (int step = 0; step < steps; step++)
        {
            simulation.Update(dt);
            double distance = simulation.GetShipDistanceToActivePrimary();

            if (distance < minDistance)
            {
                minDistance = distance;
            }

            if (distance > maxDistance)
            {
                maxDistance = distance;
            }
        }

        Console.WriteLine(label);
        Console.WriteLine("  active primary: " + simulation.activePrimaryBodyName);
        Console.WriteLine("  min distance: " + minDistance);
        Console.WriteLine("  max distance: " + maxDistance);
        Console.WriteLine("  spread: " + (maxDistance - minDistance));
        Console.WriteLine(
            "  eccentricity: " +
            simulation.GetShipOrbitalEccentricityRelativeToActivePrimary()
        );
        Console.WriteLine(
            "  periapsis: " +
            simulation.GetShipPeriapsisRelativeToActivePrimary()
        );
        Console.WriteLine(
            "  apoapsis: " +
            simulation.GetShipApoapsisRelativeToActivePrimary()
        );
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
        double eccentricity = simulation.GetShipOrbitalEccentricityRelativeToPlanet();
        double semiMajorAxis = simulation.GetShipSemiMajorAxisRelativeToPlanet();
        double periapsis = simulation.GetShipPeriapsisRelativeToPlanet();
        double apoapsis = simulation.GetShipApoapsisRelativeToPlanet();
        double activePrimaryEccentricity = simulation.GetShipOrbitalEccentricityRelativeToActivePrimary();
        double activePrimarySemiMajorAxis = simulation.GetShipSemiMajorAxisRelativeToActivePrimary();
        double activePrimaryPeriapsis = simulation.GetShipPeriapsisRelativeToActivePrimary();
        double activePrimaryApoapsis = simulation.GetShipApoapsisRelativeToActivePrimary();

        PrintSection("Session");
        PrintInfo("mode", "режим", "orbit test");
        PrintInfo("scenario", "сценарий", scenario);
        PrintInfo("steps", "шаги", steps);
        PrintInfo("dt", "шаг времени", dt);

        PrintSection("State");
        PrintInfo("ship speed", "скорость корабля", simulation.GetShipSpeed());
        PrintInfo("active primary", "активное главное тело", simulation.activePrimaryBodyName);
        PrintInfo("distance to active primary", "расстояние до активного главного тела", simulation.GetShipDistanceToActivePrimary());
        PrintInfo("active primary soi radius", "радиус сферы влияния активного тела", simulation.GetActivePrimarySphereOfInfluenceRadius());
        PrintInfo("distance to planet", "расстояние до планеты", simulation.GetShipDistanceToPlanet());
        PrintInfo("distance to moon", "расстояние до луны", simulation.GetShipDistanceToMoon());
        PrintInfo("moon speed", "скорость луны", simulation.GetMoonSpeed());
        PrintInfo("moon distance to planet", "расстояние от луны до планеты", simulation.GetMoonDistanceToPlanet());

        PrintSection("Orbit Planet");
        PrintInfo("orbit type relative to planet", "тип орбиты относительно планеты", simulation.GetShipOrbitTypeRelativeToPlanet());
        PrintInfo("escape speed relative to planet", "вторая космическая скорость относительно планеты", simulation.GetShipEscapeSpeedRelativeToPlanet());
        PrintInfo("bound to planet", "связана ли орбита с планетой", simulation.IsShipBoundToPlanet());
        PrintInfo("min distance to planet", "минимальное расстояние до планеты", simulation.minShipDistanceToPlanet);
        PrintInfo("max distance to planet", "максимальное расстояние до планеты", simulation.maxShipDistanceToPlanet);
        PrintInfo("distance spread", "разброс расстояния", simulation.maxShipDistanceToPlanet - simulation.minShipDistanceToPlanet);
        PrintInfo("eccentricity", "эксцентриситет", eccentricity);
        PrintInfo("semi-major axis", "большая полуось", semiMajorAxis);
        PrintInfo("periapsis", "перицентр", periapsis);
        PrintInfo("apoapsis", "апоцентр", apoapsis);

        PrintSection("Orbit Active Primary");
        PrintInfo("orbit type relative to active primary", "тип орбиты относительно активного главного тела", simulation.GetShipOrbitTypeRelativeToActivePrimary());
        PrintInfo("escape speed relative to active primary", "вторая космическая скорость относительно активного главного тела", simulation.GetShipEscapeSpeedRelativeToActivePrimary());
        PrintInfo("bound to active primary", "связана ли орбита с активным главным телом", simulation.IsShipBoundToActivePrimary());
        PrintInfo("active primary eccentricity", "эксцентриситет относительно активного главного тела", activePrimaryEccentricity);
        PrintInfo("active primary semi-major axis", "большая полуось относительно активного главного тела", activePrimarySemiMajorAxis);
        PrintInfo("active primary periapsis", "перицентр относительно активного главного тела", activePrimaryPeriapsis);
        PrintInfo("active primary apoapsis", "апоцентр относительно активного главного тела", activePrimaryApoapsis);

        PrintSection("Stability");
        PrintInfo("initial specific energy", "начальная удельная энергия", simulation.initialShipSpecificEnergy);
        PrintInfo("current specific energy", "текущая удельная энергия", currentEnergy);
        PrintInfo("specific energy delta", "изменение удельной энергии", currentEnergy - simulation.initialShipSpecificEnergy);
        PrintInfo("initial angular momentum", "начальный угловой момент", simulation.initialShipAngularMomentum);
        PrintInfo("current angular momentum", "текущий угловой момент", currentAngularMomentum);
        PrintInfo("angular momentum delta", "изменение углового момента", currentAngularMomentum - simulation.initialShipAngularMomentum);
    }

    // RunVisualization = запустить консольную визуализацию симуляции.
    static void RunVisualization(SimulationScenario scenario)
    {
        double dt = 0.5;

        Simulation simulation = new Simulation(scenario);
        bool[,] trail = new bool[ScreenHeight, ScreenWidth];
        bool isRunning = true;
        int step = 0;
        Vector2 plannedDeltaV = Vector2.Zero();
        string plannedManeuver = "none";
        string lastAppliedManeuver = "none";
        double lastAppliedDeltaV = 0;
        double lastPeriapsisDelta = 0;
        double lastApoapsisDelta = 0;
        double lastEccentricityDelta = 0;
        double lastSemiMajorAxisDelta = 0;

        while (isRunning)
        {
            ControlInput control = ReadManualControl(simulation, ref isRunning);
            simulation.SetShipThrust(control.thrust);

            if (control.previewDeltaV.LengthSquared() > 0)
            {
                plannedDeltaV = plannedDeltaV.Add(control.previewDeltaV);
                plannedManeuver = control.actionName;
            }

            if (control.clearPlannedManeuver)
            {
                plannedDeltaV = Vector2.Zero();
                plannedManeuver = "none";
            }

            if (control.applyPlannedManeuver && plannedDeltaV.LengthSquared() > 0)
            {
                double periapsisBefore = simulation.GetShipPeriapsisRelativeToActivePrimary();
                double apoapsisBefore = simulation.GetShipApoapsisRelativeToActivePrimary();
                double eccentricityBefore = simulation.GetShipOrbitalEccentricityRelativeToActivePrimary();
                double semiMajorAxisBefore = simulation.GetShipSemiMajorAxisRelativeToActivePrimary();

                simulation.ApplyShipDeltaV(plannedDeltaV);

                double periapsisAfter = simulation.GetShipPeriapsisRelativeToActivePrimary();
                double apoapsisAfter = simulation.GetShipApoapsisRelativeToActivePrimary();
                double eccentricityAfter = simulation.GetShipOrbitalEccentricityRelativeToActivePrimary();
                double semiMajorAxisAfter = simulation.GetShipSemiMajorAxisRelativeToActivePrimary();

                lastAppliedManeuver = plannedManeuver;
                lastAppliedDeltaV = plannedDeltaV.Length();
                lastPeriapsisDelta = periapsisAfter - periapsisBefore;
                lastApoapsisDelta = apoapsisAfter - apoapsisBefore;
                lastEccentricityDelta = eccentricityAfter - eccentricityBefore;
                lastSemiMajorAxisDelta = semiMajorAxisAfter - semiMajorAxisBefore;

                plannedDeltaV = Vector2.Zero();
                plannedManeuver = "none";
            }

            simulation.Update(dt);

            Draw(
                simulation.ship.position,
                simulation.planet.position,
                simulation.moon.position,
                scenario == SimulationScenario.PlanetMoonShip ||
                scenario == SimulationScenario.MoonFlyby,
                trail
            );

            PrintSection("Session");
            PrintInfo("mode", "режим", "visualization");
            PrintInfo("scenario", "сценарий", scenario);
            PrintInfo("controls", "управление", "W A S D thrust, Q/E pro-retro thrust, R/F radial thrust, I/K/O/L plan impulse, Enter apply plan, Backspace clear plan, Space reset thrust, Esc exit");
            PrintInfo("time", "время", (step * dt) + " sec");

            PrintSection("State");
            PrintInfo("ship speed", "скорость корабля", simulation.GetShipSpeed());
            PrintInfo("distance to planet", "расстояние до планеты", simulation.GetShipDistanceToPlanet());
            PrintInfo("distance to moon", "расстояние до луны", simulation.GetShipDistanceToMoon());
            PrintInfo("active primary", "активное главное тело", simulation.activePrimaryBodyName);
            PrintInfo("distance to active primary", "расстояние до активного главного тела", simulation.GetShipDistanceToActivePrimary());
            PrintInfo("active primary soi radius", "радиус сферы влияния активного тела", simulation.GetActivePrimarySphereOfInfluenceRadius());
            PrintInfo("gravity acceleration", "гравитационное ускорение", simulation.lastGravityAcceleration.Length());

            PrintSection("Orbit Planet");
            PrintInfo("orbit type relative to planet", "тип орбиты относительно планеты", simulation.GetShipOrbitTypeRelativeToPlanet());
            PrintInfo("escape speed relative to planet", "вторая космическая скорость относительно планеты", simulation.GetShipEscapeSpeedRelativeToPlanet());
            PrintInfo("bound to planet", "связана ли орбита с планетой", simulation.IsShipBoundToPlanet());
            PrintInfo("eccentricity", "эксцентриситет", simulation.GetShipOrbitalEccentricityRelativeToPlanet());
            PrintInfo("semi-major axis", "большая полуось", simulation.GetShipSemiMajorAxisRelativeToPlanet());
            PrintInfo("periapsis", "перицентр", simulation.GetShipPeriapsisRelativeToPlanet());
            PrintInfo("apoapsis", "апоцентр", simulation.GetShipApoapsisRelativeToPlanet());
            PrintInfo("min distance to planet", "минимальное расстояние до планеты", simulation.minShipDistanceToPlanet);
            PrintInfo("max distance to planet", "максимальное расстояние до планеты", simulation.maxShipDistanceToPlanet);

            PrintSection("Orbit Active Primary");
            PrintInfo("orbit type relative to active primary", "тип орбиты относительно активного главного тела", simulation.GetShipOrbitTypeRelativeToActivePrimary());
            PrintInfo("escape speed relative to active primary", "вторая космическая скорость относительно активного главного тела", simulation.GetShipEscapeSpeedRelativeToActivePrimary());
            PrintInfo("bound to active primary", "связана ли орбита с активным главным телом", simulation.IsShipBoundToActivePrimary());
            PrintInfo("active primary eccentricity", "эксцентриситет относительно активного главного тела", simulation.GetShipOrbitalEccentricityRelativeToActivePrimary());
            PrintInfo("active primary semi-major axis", "большая полуось относительно активного главного тела", simulation.GetShipSemiMajorAxisRelativeToActivePrimary());
            PrintInfo("active primary periapsis", "перицентр относительно активного главного тела", simulation.GetShipPeriapsisRelativeToActivePrimary());
            PrintInfo("active primary apoapsis", "апоцентр относительно активного главного тела", simulation.GetShipApoapsisRelativeToActivePrimary());

            PrintSection("Control");
            PrintInfo("thrust x", "тяга по x", simulation.ship.thrust.x);
            PrintInfo("thrust y", "тяга по y", simulation.ship.thrust.y);
            PrintInfo("planned maneuver", "запланированный манёвр", plannedManeuver);
            PrintInfo("planned delta-v", "запланированный delta-v", plannedDeltaV.Length());
            PrintInfo("planned delta-v x", "запланированный delta-v по x", plannedDeltaV.x);
            PrintInfo("planned delta-v y", "запланированный delta-v по y", plannedDeltaV.y);

            PrintSection("Preview");
            PrintInfo("preview orbit type", "тип орбиты после плана", simulation.GetPreviewShipOrbitTypeRelativeToActivePrimary(plannedDeltaV));
            PrintInfo("preview eccentricity", "эксцентриситет после плана", simulation.GetPreviewShipOrbitalEccentricityRelativeToActivePrimary(plannedDeltaV));
            PrintInfo("preview semi-major axis", "большая полуось после плана", simulation.GetPreviewShipSemiMajorAxisRelativeToActivePrimary(plannedDeltaV));
            PrintInfo("preview periapsis", "перицентр после плана", simulation.GetPreviewShipPeriapsisRelativeToActivePrimary(plannedDeltaV));
            PrintInfo("preview apoapsis", "апоцентр после плана", simulation.GetPreviewShipApoapsisRelativeToActivePrimary(plannedDeltaV));
            PrintInfo("preview periapsis delta", "изменение перицентра после плана", simulation.GetPreviewShipPeriapsisRelativeToActivePrimary(plannedDeltaV) - simulation.GetShipPeriapsisRelativeToActivePrimary());
            PrintInfo("preview apoapsis delta", "изменение апоцентра после плана", simulation.GetPreviewShipApoapsisRelativeToActivePrimary(plannedDeltaV) - simulation.GetShipApoapsisRelativeToActivePrimary());

            PrintSection("Last Applied");
            PrintInfo("last maneuver", "последний применённый манёвр", lastAppliedManeuver);
            PrintInfo("last impulse delta-v", "последний применённый delta-v", lastAppliedDeltaV);
            PrintInfo("periapsis delta", "изменение перицентра", lastPeriapsisDelta);
            PrintInfo("apoapsis delta", "изменение апоцентра", lastApoapsisDelta);
            PrintInfo("eccentricity delta", "изменение эксцентриситета", lastEccentricityDelta);
            PrintInfo("semi-major axis delta", "изменение большой полуоси", lastSemiMajorAxisDelta);

            Thread.Sleep(200);
            step++;
        }
    }

    // ReadManualControl = прочитать ручное управление thrust и импульсами delta-v для текущего кадра.
    static ControlInput ReadManualControl(Simulation simulation, ref bool isRunning)
    {
        ControlInput control = new ControlInput();
        control.thrust = Vector2.Zero();
        control.previewDeltaV = Vector2.Zero();
        control.actionName = "none";
        control.applyPlannedManeuver = false;
        control.clearPlannedManeuver = false;

        try
        {
            while (Console.KeyAvailable)
            {
                ConsoleKey key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.Escape)
                {
                    isRunning = false;
                }
                else if (key == ConsoleKey.Spacebar)
                {
                    control.thrust = Vector2.Zero();
                    control.actionName = "reset thrust";
                }
                else if (key == ConsoleKey.Enter)
                {
                    control.applyPlannedManeuver = true;
                    control.actionName = "apply planned maneuver";
                }
                else if (key == ConsoleKey.Backspace)
                {
                    control.clearPlannedManeuver = true;
                    control.actionName = "clear planned maneuver";
                }
                else if (key == ConsoleKey.W)
                {
                    control.thrust = control.thrust.Add(new Vector2(0, ManualThrustPower));
                    control.actionName = "world thrust up";
                }
                else if (key == ConsoleKey.S)
                {
                    control.thrust = control.thrust.Add(new Vector2(0, -ManualThrustPower));
                    control.actionName = "world thrust down";
                }
                else if (key == ConsoleKey.A)
                {
                    control.thrust = control.thrust.Add(new Vector2(-ManualThrustPower, 0));
                    control.actionName = "world thrust left";
                }
                else if (key == ConsoleKey.D)
                {
                    control.thrust = control.thrust.Add(new Vector2(ManualThrustPower, 0));
                    control.actionName = "world thrust right";
                }
                else if (key == ConsoleKey.Q)
                {
                    control.thrust = control.thrust.Add(GetProgradeThrust(simulation));
                    control.actionName = "prograde thrust";
                }
                else if (key == ConsoleKey.E)
                {
                    control.thrust = control.thrust.Add(GetRetrogradeThrust(simulation));
                    control.actionName = "retrograde thrust";
                }
                else if (key == ConsoleKey.R)
                {
                    control.thrust = control.thrust.Add(GetRadialOutThrust(simulation));
                    control.actionName = "radial out thrust";
                }
                else if (key == ConsoleKey.F)
                {
                    control.thrust = control.thrust.Add(GetRadialInThrust(simulation));
                    control.actionName = "radial in thrust";
                }
                else if (key == ConsoleKey.I)
                {
                    control.previewDeltaV = control.previewDeltaV.Add(GetProgradeImpulse(simulation));
                    control.actionName = "plan prograde impulse";
                }
                else if (key == ConsoleKey.K)
                {
                    control.previewDeltaV = control.previewDeltaV.Add(GetRetrogradeImpulse(simulation));
                    control.actionName = "plan retrograde impulse";
                }
                else if (key == ConsoleKey.O)
                {
                    control.previewDeltaV = control.previewDeltaV.Add(GetRadialOutImpulse(simulation));
                    control.actionName = "plan radial out impulse";
                }
                else if (key == ConsoleKey.L)
                {
                    control.previewDeltaV = control.previewDeltaV.Add(GetRadialInImpulse(simulation));
                    control.actionName = "plan radial in impulse";
                }
            }
        }
        catch (InvalidOperationException)
        {
            return control;
        }

        return control;
    }

    // GetProgradeThrust = получить тягу по направлению текущей скорости корабля.
    static Vector2 GetProgradeThrust(Simulation simulation)
    {
        Vector2 direction = simulation.GetShipProgradeDirectionRelativeToActivePrimary();
        return direction.Multiply(ManualThrustPower);
    }

    // GetRetrogradeThrust = получить тягу против направления текущей скорости корабля.
    static Vector2 GetRetrogradeThrust(Simulation simulation)
    {
        Vector2 direction = simulation.GetShipRetrogradeDirectionRelativeToActivePrimary();
        return direction.Multiply(ManualThrustPower);
    }

    // GetRadialOutThrust = получить тягу от активного главного тела наружу.
    static Vector2 GetRadialOutThrust(Simulation simulation)
    {
        Vector2 direction = simulation.GetShipRadialOutDirectionRelativeToActivePrimary();
        return direction.Multiply(ManualThrustPower);
    }

    // GetRadialInThrust = получить тягу к активному главному телу.
    static Vector2 GetRadialInThrust(Simulation simulation)
    {
        Vector2 direction = simulation.GetShipRadialInDirectionRelativeToActivePrimary();
        return direction.Multiply(ManualThrustPower);
    }

    // GetProgradeImpulse = получить импульс delta-v по скорости.
    static Vector2 GetProgradeImpulse(Simulation simulation)
    {
        Vector2 direction = simulation.GetShipProgradeDirectionRelativeToActivePrimary();
        return direction.Multiply(ManualImpulseDeltaV);
    }

    // GetRetrogradeImpulse = получить импульс delta-v против скорости.
    static Vector2 GetRetrogradeImpulse(Simulation simulation)
    {
        Vector2 direction = simulation.GetShipRetrogradeDirectionRelativeToActivePrimary();
        return direction.Multiply(ManualImpulseDeltaV);
    }

    // GetRadialOutImpulse = получить импульс delta-v от активного главного тела наружу.
    static Vector2 GetRadialOutImpulse(Simulation simulation)
    {
        Vector2 direction = simulation.GetShipRadialOutDirectionRelativeToActivePrimary();
        return direction.Multiply(ManualImpulseDeltaV);
    }

    // GetRadialInImpulse = получить импульс delta-v к активному главному телу.
    static Vector2 GetRadialInImpulse(Simulation simulation)
    {
        Vector2 direction = simulation.GetShipRadialInDirectionRelativeToActivePrimary();
        return direction.Multiply(ManualImpulseDeltaV);
    }

    static void Draw(
        Vector2 shipPosition,
        Vector2 planetPosition,
        Vector2 moonPosition,
        bool drawMoon,
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
                else if (drawMoon && x == moonX && y == moonY)
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

struct ControlInput
{
    public Vector2 thrust;
    public Vector2 previewDeltaV;
    public string actionName;
    public bool applyPlannedManeuver;
    public bool clearPlannedManeuver;
}
