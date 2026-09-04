const int DefaultPort = 27777;

int port = DefaultPort;
SimulationScenario scenario = SimulationScenario.MoonFlyby;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[i + 1], out int parsedPort))
    {
        port = parsedPort;
        i++;
        continue;
    }

    if (args[i] == "--scenario" && i + 1 < args.Length)
    {
        if (Enum.TryParse(args[i + 1], ignoreCase: true, out SimulationScenario parsedScenario))
        {
            scenario = parsedScenario;
        }

        i++;
    }
}

using LockstepServer server = new LockstepServer(port, scenario);

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    server.Stop();
};

server.Run();
