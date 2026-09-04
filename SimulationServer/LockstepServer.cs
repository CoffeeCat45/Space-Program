using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

sealed class LockstepServer : IDisposable
{
    const double FixedDt = 0.02;
    const int TickRateHz = 50;
    const long TickIntervalMs = 1000 / TickRateHz;
    const int MaxPlayerSlots = 2;

    readonly UdpClient udp;
    readonly Simulation simulation;
    readonly ConcurrentDictionary<long, PendingTickInputs> pendingInputs = new();
    readonly ConcurrentDictionary<string, IPEndPoint> clients = new();
    readonly ConcurrentDictionary<string, int> clientPlayerSlots = new();
    readonly ConcurrentDictionary<string, bool> clientsSnapshotSent = new();
    readonly object simulationLock = new();

    long serverTick;
    long serverIdleLogCounter;
    long serverStatusLogCounter;
    bool running;

    NetworkInputFrame lastPlayer1Input;
    NetworkInputFrame lastPlayer2Input;
    bool hasLastPlayer1Input;
    bool hasLastPlayer2Input;

    sealed class PendingTickInputs
    {
        public bool hasPlayer1;
        public bool hasPlayer2;
        public NetworkInputFrame player1;
        public NetworkInputFrame player2;
    }

    public LockstepServer(int port, SimulationScenario scenario)
    {
        udp = new UdpClient(port);
        simulation = new Simulation(scenario);
        simulation.IsServerContext = true;
        serverTick = simulation.currentTick;
    }

    public void Run()
    {
        running = true;
        udp.BeginReceive(OnDatagramReceived, null);

        Console.WriteLine(
            "Lockstep server listening on UDP port {0}, scenario {1}, {2} Hz",
            ((IPEndPoint)udp.Client.LocalEndPoint!).Port,
            simulation.scenario,
            TickRateHz
        );

        long nextTickAtMs = Environment.TickCount64;

        while (running)
        {
            long nowMs = Environment.TickCount64;

            if (nowMs >= nextTickAtMs)
            {
                AdvanceSimulationTick();
                nextTickAtMs += TickIntervalMs;
            }

            Thread.Sleep(1);
        }
    }

    public void Stop()
    {
        running = false;
    }

    void OnDatagramReceived(IAsyncResult result)
    {
        if (!running)
        {
            return;
        }

        try
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = udp.EndReceive(result, ref remote);
            string clientKey = remote.ToString();
            clients[clientKey] = remote;

            Console.WriteLine(
                "UDP {0} bytes from {1} (first byte 0x{2:X2})",
                data.Length,
                remote,
                data.Length > 0 ? data[0] : 0
            );

            if (NetworkTickPacket.IsClientJoinPacket(data, data.Length))
            {
                HandleClientJoin(clientKey, remote);
                return;
            }

            if (
                data.Length > 0 &&
                data[0] == NetworkTickPacket.PacketTypeClientInput &&
                NetworkTickPacket.TryDeserialize(data, data.Length, out NetworkTickPacket packet)
            )
            {
                NetworkInputFrame frame = packet.player1Frame;

                if (!clientPlayerSlots.TryGetValue(clientKey, out int playerSlot))
                {
                    return;
                }

                pendingInputs.AddOrUpdate(
                    frame.tick,
                    _ => CreatePendingInputs(playerSlot, frame),
                    (_, pending) =>
                    {
                        StoreInputForSlot(pending, playerSlot, frame);
                        return pending;
                    }
                );
                CachePlayerInput(playerSlot, frame);
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine("Receive error: " + exception.Message);
        }
        finally
        {
            if (running)
            {
                udp.BeginReceive(OnDatagramReceived, null);
            }
        }
    }

    void HandleClientJoin(string clientKey, IPEndPoint remote)
    {
        if (!clientsSnapshotSent.ContainsKey(clientKey))
        {
            int playerSlot = AssignClientPlayerSlot(clientKey);
            clientsSnapshotSent[clientKey] = true;
            SendWorldSnapshot(remote);
            SendClientSlotAssigned(remote, playerSlot);
            Console.WriteLine(
                "Client {0} joined as player slot {1}",
                remote,
                playerSlot
            );
        }
    }

    static PendingTickInputs CreatePendingInputs(int playerSlot, NetworkInputFrame frame)
    {
        PendingTickInputs pending = new PendingTickInputs();
        StoreInputForSlot(pending, playerSlot, frame);
        return pending;
    }

    static void StoreInputForSlot(
        PendingTickInputs pending,
        int playerSlot,
        NetworkInputFrame frame
    )
    {
        if (playerSlot == NetworkInputFrame.Player1VesselId)
        {
            pending.hasPlayer1 = true;
            pending.player1 = frame;
            return;
        }

        if (playerSlot == NetworkInputFrame.Player2VesselId)
        {
            pending.hasPlayer2 = true;
            pending.player2 = frame;
        }
    }

    int AssignClientPlayerSlot(string clientKey)
    {
        if (clientPlayerSlots.TryGetValue(clientKey, out int existingSlot))
        {
            return existingSlot;
        }

        int assignedCount = 0;

        foreach (KeyValuePair<string, int> entry in clientPlayerSlots)
        {
            assignedCount++;
        }

        int playerSlot = Math.Clamp(assignedCount + 1, 1, MaxPlayerSlots);
        clientPlayerSlots[clientKey] = playerSlot;
        return playerSlot;
    }

    void SendClientSlotAssigned(IPEndPoint remote, int playerSlot)
    {
        byte[] payload = NetworkTickPacket.SerializeClientSlotAssigned(playerSlot);

        try
        {
            udp.Send(payload, payload.Length, remote);
        }
        catch (Exception exception)
        {
            Console.WriteLine("Slot assignment send error to " + remote + ": " + exception.Message);
        }
    }

    void SendWorldSnapshot(IPEndPoint remote)
    {
        byte[] snapshotPayload;

        lock (simulationLock)
        {
            snapshotPayload = ShipPersistence.SerializeToBinary(simulation.CaptureSnapshot());
        }

        byte[] payload = NetworkTickPacket.SerializeWorldSnapshot(snapshotPayload);

        try
        {
            udp.Send(payload, payload.Length, remote);
            Console.WriteLine("Sent join snapshot to " + remote + " (" + snapshotPayload.Length + " bytes)");
        }
        catch (Exception exception)
        {
            Console.WriteLine("Snapshot send error to " + remote + ": " + exception.Message);
        }
    }

    bool IsPlayerSlotConnected(int playerSlot)
    {
        foreach (KeyValuePair<string, int> entry in clientPlayerSlots)
        {
            if (entry.Value == playerSlot)
            {
                return true;
            }
        }

        return false;
    }

    void CachePlayerInput(int playerSlot, NetworkInputFrame frame)
    {
        if (playerSlot == NetworkInputFrame.Player1VesselId)
        {
            lastPlayer1Input = frame;
            hasLastPlayer1Input = true;
            return;
        }

        if (playerSlot == NetworkInputFrame.Player2VesselId)
        {
            lastPlayer2Input = frame;
            hasLastPlayer2Input = true;
        }
    }

    bool HasCachedPlayerInput(int playerSlot)
    {
        if (playerSlot == NetworkInputFrame.Player1VesselId)
        {
            return hasLastPlayer1Input;
        }

        if (playerSlot == NetworkInputFrame.Player2VesselId)
        {
            return hasLastPlayer2Input;
        }

        return false;
    }

    bool TryGetCachedPlayerInput(int playerSlot, out NetworkInputFrame frame)
    {
        if (playerSlot == NetworkInputFrame.Player1VesselId && hasLastPlayer1Input)
        {
            frame = lastPlayer1Input;
            return true;
        }

        if (playerSlot == NetworkInputFrame.Player2VesselId && hasLastPlayer2Input)
        {
            frame = lastPlayer2Input;
            return true;
        }

        frame = default;
        return false;
    }

    bool HasPlayerInputForTick(int playerSlot, PendingTickInputs pending)
    {
        if (playerSlot == NetworkInputFrame.Player1VesselId)
        {
            return pending.hasPlayer1 || hasLastPlayer1Input;
        }

        if (playerSlot == NetworkInputFrame.Player2VesselId)
        {
            return pending.hasPlayer2 || hasLastPlayer2Input;
        }

        return false;
    }

    NetworkInputFrame ResolveSlotFrame(
        int playerSlot,
        bool hasBufferedInput,
        NetworkInputFrame bufferedFrame
    )
    {
        if (!IsPlayerSlotConnected(playerSlot))
        {
            return NetworkInputFrame.CreateIdle(playerSlot, serverTick);
        }

        if (hasBufferedInput)
        {
            CachePlayerInput(playerSlot, bufferedFrame);
            return bufferedFrame;
        }

        if (TryGetCachedPlayerInput(playerSlot, out NetworkInputFrame cachedFrame))
        {
            cachedFrame.tick = serverTick;
            return cachedFrame;
        }

        return NetworkInputFrame.CreateIdle(playerSlot, serverTick);
    }

    void AdvanceSimulationTick()
    {
        if (clients.Count == 0)
        {
            serverTick = 0;
            serverIdleLogCounter++;

            if (serverIdleLogCounter % TickRateHz == 0)
            {
                Console.WriteLine(
                    "tick=0 checksum=0x{0:X16} pause=True clients=0 p1=False p2=False",
                    simulation.CalculateStateChecksum()
                );
                serverIdleLogCounter = 0;
            }

            return;
        }

        PendingTickInputs pending = new PendingTickInputs();

        if (pendingInputs.TryRemove(serverTick, out PendingTickInputs? bufferedInputs))
        {
            pending = bufferedInputs;
        }

        NetworkInputFrame player1Frame = ResolveSlotFrame(
            NetworkInputFrame.Player1VesselId,
            pending.hasPlayer1,
            pending.player1
        );
        NetworkInputFrame player2Frame = ResolveSlotFrame(
            NetworkInputFrame.Player2VesselId,
            pending.hasPlayer2,
            pending.player2
        );

        bool player1Connected = IsPlayerSlotConnected(NetworkInputFrame.Player1VesselId);
        bool player2Connected = IsPlayerSlotConnected(NetworkInputFrame.Player2VesselId);

        bool pauseTick =
            player1Frame.isPauseRequested ||
            player2Frame.isPauseRequested;

        if (serverTick > 0)
        {
            if (player1Connected && !HasPlayerInputForTick(NetworkInputFrame.Player1VesselId, pending))
            {
                pauseTick = true;
            }

            if (player2Connected && !HasPlayerInputForTick(NetworkInputFrame.Player2VesselId, pending))
            {
                pauseTick = true;
            }
        }
        else
        {
            if (player1Connected && player1Frame.isPauseRequested)
            {
                pauseTick = true;
            }

            if (player2Connected && player2Frame.isPauseRequested)
            {
                pauseTick = true;
            }
        }

        ulong checksum;
        List<VesselCompactState> correctionStates;

        lock (simulationLock)
        {
            if (!pauseTick)
            {
                NetworkInputDefaults.ApplyLockstepTickInputs(
                    simulation,
                    serverTick,
                    player1Frame,
                    player2Frame
                );
                simulation.Update(FixedDt);
                serverTick = simulation.currentTick;
            }

            checksum = simulation.CalculateStateChecksum();
            correctionStates = BuildCorrectionStates();
        }

        NetworkTickPacket broadcast = new NetworkTickPacket
        {
            tick = serverTick,
            stateChecksum = checksum,
            player1Frame = player1Frame,
            player2Frame = player2Frame,
            correctionStates = correctionStates,
        };

        byte[] payload = broadcast.SerializeServerTick();

        foreach (KeyValuePair<string, IPEndPoint> client in clients)
        {
            try
            {
                udp.Send(payload, payload.Length, client.Value);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Send error to " + client.Key + ": " + exception.Message);
            }
        }

        serverStatusLogCounter++;

        if (serverStatusLogCounter % TickRateHz == 0)
        {
            Console.WriteLine(
                "tick={0} checksum=0x{1:X16} pause={2} clients={3} p1={4} p2={5}",
                serverTick,
                checksum,
                pauseTick,
                clients.Count,
                player1Connected,
                player2Connected
            );
            serverStatusLogCounter = 0;
        }
    }

    List<VesselCompactState> BuildCorrectionStates()
    {
        List<VesselCompactState> states = new List<VesselCompactState>();

        for (int i = 0; i < simulation.vessels.Count; i++)
        {
            Vessel vessel = simulation.vessels[i];

            if (vessel == null || vessel.IsDestroyed)
            {
                continue;
            }

            states.Add(
                new VesselCompactState
                {
                    id = simulation.GetVesselId(vessel),
                    pos = vessel.position,
                    vel = vessel.velocity,
                }
            );
        }

        return states;
    }

    public void Dispose()
    {
        running = false;
        udp.Close();
    }
}
