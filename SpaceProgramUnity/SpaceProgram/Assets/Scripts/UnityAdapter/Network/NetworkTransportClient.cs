using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
class NetworkTransportClient : MonoBehaviour
{
    public int serverPort = 27777;

    SimulationRunner runner;
    UdpClient udpClient;
    IPEndPoint serverEndpoint;
    Thread receiveThread;
    volatile bool receiveThreadRunning;
    readonly object receiveLock = new object();
    readonly System.Diagnostics.Stopwatch netStopwatch = new System.Diagnostics.Stopwatch();
    volatile float lastPacketReceiveSeconds;

    void Start()
    {
        netStopwatch.Start();
    }

    void Awake()
    {
        runner = GetComponent<SimulationRunner>();

        if (runner == null)
        {
            Debug.LogError("NetworkTransportClient requires SimulationRunner on the same GameObject.");
            enabled = false;
        }
    }

    void OnDisable()
    {
        StopTransport();
    }

    public void StartTransport(string host, int port)
    {
        StopTransport();

        if (runner == null || string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        try
        {
            serverPort = port;
            serverEndpoint = new IPEndPoint(IPAddress.Parse(host.Trim()), port);
            udpClient = new UdpClient(0);
            udpClient.Connect(serverEndpoint);
            receiveThreadRunning = true;
            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "NetworkTransportReceive",
            };
            receiveThread.Start();
            enabled = true;
            SendJoinPacket();
            BuildAndSendLocalInputFrame();
            Debug.Log(
                "NetworkTransportClient connected to " +
                serverEndpoint +
                " (join packet sent)."
            );
        }
        catch (Exception exception)
        {
            Debug.LogError("NetworkTransportClient failed to start: " + exception.Message);
            enabled = false;
        }
    }

    public void SendJoinPacket()
    {
        if (udpClient == null)
        {
            return;
        }

        try
        {
            byte[] payload = NetworkTickPacket.SerializeClientJoin();
            udpClient.Send(payload, payload.Length);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("NetworkTransportClient join send failed: " + exception.Message);
        }
    }

    public void BuildAndSendLocalInputFrame()
    {
        if (
            udpClient == null ||
            serverEndpoint == null ||
            runner == null ||
            !runner.IsMultiplayerMode ||
            runner.Simulation == null ||
            runner.GetAssignedPlayerSlot() <= 0 ||
            !runner.IsLockstepReady
        )
        {
            return;
        }

        NetworkInputFrame frame = BuildLocalInputFrame();
        NetworkTickPacket packet = new NetworkTickPacket
        {
            player1Frame = frame,
        };

        try
        {
            byte[] payload = packet.SerializeClientInput();
            udpClient.Send(payload, payload.Length);

            if (runner.HasPendingPauseToggleToSend())
            {
                runner.AcknowledgePauseInputSent();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("NetworkTransportClient send failed: " + exception.Message);
        }
    }

    public void StopTransport()
    {
        receiveThreadRunning = false;

        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
            }
            catch (Exception)
            {
            }

            udpClient = null;
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(250);
            receiveThread = null;
        }
    }

    void Update()
    {
        if (
            !enabled ||
            runner == null ||
            !runner.IsMultiplayerMode
        )
        {
            return;
        }

        if (!runner.IsLockstepReady)
        {
            SendJoinPacket();
        }
    }

    NetworkInputFrame BuildLocalInputFrame()
    {
        Simulation simulation = runner.Simulation;
        Keyboard keyboard = Keyboard.current;
        int playerSlot = runner.GetAssignedPlayerSlot();
        bool pauseRequested = runner.GetLocalNetworkPauseRequestStatus();

        float pitchInput = 0.0f;
        float yawInput = 0.0f;
        float rollInput = 0.0f;

        if (keyboard != null)
        {
            pitchInput = ReadAxisInput(
                keyboard.sKey != null && keyboard.sKey.isPressed,
                keyboard.wKey != null && keyboard.wKey.isPressed
            );
            yawInput = ReadAxisInput(
                keyboard.aKey != null && keyboard.aKey.isPressed,
                keyboard.dKey != null && keyboard.dKey.isPressed
            );
            rollInput = ReadAxisInput(
                keyboard.eKey != null && keyboard.eKey.isPressed,
                keyboard.qKey != null && keyboard.qKey.isPressed
            );
        }

        return new NetworkInputFrame
        {
            // Отправляем ввод с упреждением в 3 тика вперед, чтобы разорвать мертвую петлю (Deadlock)
            tick = simulation.currentTick + 3,
            vesselId = playerSlot,
            throttle = runner.Throttle,
            staging = runner.ConsumePendingNetworkStaging(),
            rcsVector = runner.GetLocalNetworkRcsVector(),
            orientationMode = runner.ShipOrientationMode,
            pitchInput = pitchInput,
            yawInput = yawInput,
            rollInput = rollInput,
            isPauseRequested = pauseRequested,
        };
    }

    static float ReadAxisInput(bool positivePressed, bool negativePressed)
    {
        if (positivePressed && !negativePressed)
        {
            return 1.0f;
        }

        if (negativePressed && !positivePressed)
        {
            return -1.0f;
        }

        return 0.0f;
    }

    void ReceiveLoop()
    {
        UdpClient localClient = udpClient;

        while (receiveThreadRunning && localClient != null)
        {
            try
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data;

                lock (receiveLock)
                {
                    if (!receiveThreadRunning || udpClient == null)
                    {
                        break;
                    }

                    data = udpClient.Receive(ref remote);
                }

                if (!NetworkTickPacket.TryDeserialize(data, data.Length, out NetworkTickPacket packet))
                {
                    continue;
                }

                if (packet.snapshotData != null && packet.snapshotData.Length > 0)
                {
                    runner.EnqueueJoinSnapshot(packet.snapshotData);
                    continue;
                }

                if (packet.assignedPlayerSlot > 0)
                {
                    runner.SetAssignedPlayerSlot(packet.assignedPlayerSlot);
                    continue;
                }

                if (packet.stateChecksum != 0UL)
                {
                    lastPacketReceiveSeconds =
                        (float)(netStopwatch.ElapsedMilliseconds / 1000.0);
                    runner.EnqueueNetworkTickPacket(packet);
                }
            }
            catch (SocketException)
            {
                if (!receiveThreadRunning)
                {
                    break;
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception exception)
            {
                if (receiveThreadRunning)
                {
                    Debug.LogWarning("NetworkTransportClient receive failed: " + exception.Message);
                }
            }
        }
    }
}
