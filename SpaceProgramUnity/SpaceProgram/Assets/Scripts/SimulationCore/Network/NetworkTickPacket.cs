using System;
using System.Collections.Generic;
using System.IO;

struct NetworkTickPacket
{
    public const byte PacketTypeClientInput = 0;
    public const byte PacketTypeServerTick = 1;
    public const byte PacketTypeWorldSnapshot = 2;
    public const byte PacketTypeClientJoin = 3;
    public const byte PacketTypeClientSlotAssigned = 6;

    public long tick;
    public ulong stateChecksum;
    public NetworkInputFrame player1Frame;
    public NetworkInputFrame player2Frame;
    public byte[] snapshotData;
    public int assignedPlayerSlot;
    public List<VesselCompactState> correctionStates;

    public bool IsPauseTick =>
        player1Frame.isPauseRequested || player2Frame.isPauseRequested;

    public static byte[] SerializeClientJoin()
    {
        return new byte[] { PacketTypeClientJoin };
    }

    public static bool IsClientJoinPacket(byte[] data, int length)
    {
        return data != null && length >= 1 && data[0] == PacketTypeClientJoin;
    }

    public static byte[] SerializeClientSlotAssigned(int playerSlot)
    {
        return new byte[]
        {
            PacketTypeClientSlotAssigned,
            (byte)Math.Clamp(playerSlot, 1, 2),
        };
    }

    public static bool TryDeserializeClientSlotAssigned(byte[] data, int length, out int playerSlot)
    {
        playerSlot = 0;

        if (data == null || length < 2 || data[0] != PacketTypeClientSlotAssigned)
        {
            return false;
        }

        playerSlot = data[1];
        return playerSlot == 1 || playerSlot == 2;
    }

    public byte[] SerializeClientInput()
    {
        using MemoryStream stream = new MemoryStream(2 + NetworkInputFrame.SerializedSize);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(PacketTypeClientInput);
        player1Frame.Write(writer);
        return stream.ToArray();
    }

    public byte[] SerializeServerTick()
    {
        int correctionCount = correctionStates == null ? 0 : correctionStates.Count;
        int payloadSize =
            1 + 8 + 8 + NetworkInputFrame.SerializedSize * 2 + 4 +
            correctionCount * VesselCompactState.SerializedSize;

        using MemoryStream stream = new MemoryStream(payloadSize);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(PacketTypeServerTick);
        writer.Write(tick);
        writer.Write(stateChecksum);
        player1Frame.Write(writer);
        player2Frame.Write(writer);
        writer.Write(correctionCount);

        for (int i = 0; i < correctionCount; i++)
        {
            correctionStates[i].Write(writer);
        }

        return stream.ToArray();
    }

    static List<VesselCompactState> ReadCorrectionStates(BinaryReader reader)
    {
        if (reader.BaseStream.Position >= reader.BaseStream.Length)
        {
            return null;
        }

        int correctionCount = reader.ReadInt32();

        if (correctionCount < 0)
        {
            return null;
        }

        List<VesselCompactState> states = new List<VesselCompactState>(correctionCount);

        for (int i = 0; i < correctionCount; i++)
        {
            states.Add(VesselCompactState.Read(reader));
        }

        return states;
    }

    public static byte[] SerializeWorldSnapshot(byte[] snapshotData)
    {
        if (snapshotData == null)
        {
            throw new ArgumentNullException(nameof(snapshotData));
        }

        if (snapshotData.Length > int.MaxValue - 5)
        {
            throw new InvalidOperationException("Snapshot payload is too large.");
        }

        using MemoryStream stream = new MemoryStream(5 + snapshotData.Length);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(PacketTypeWorldSnapshot);
        writer.Write(snapshotData.Length);
        writer.Write(snapshotData);
        return stream.ToArray();
    }

    public static bool TryDeserialize(byte[] data, int length, out NetworkTickPacket packet)
    {
        packet = default;

        if (data == null || length < 1)
        {
            return false;
        }

        using MemoryStream stream = new MemoryStream(data, 0, length, writable: false);
        using BinaryReader reader = new BinaryReader(stream);

        byte packetType = reader.ReadByte();

        if (packetType == PacketTypeClientSlotAssigned)
        {
            if (length < 2)
            {
                return false;
            }

            packet = new NetworkTickPacket
            {
                assignedPlayerSlot = reader.ReadByte(),
            };
            return packet.assignedPlayerSlot == 1 || packet.assignedPlayerSlot == 2;
        }

        if (packetType == PacketTypeClientJoin)
        {
            packet = new NetworkTickPacket
            {
                tick = 0L,
                stateChecksum = 0UL,
            };
            return true;
        }

        if (packetType == PacketTypeClientInput)
        {
            NetworkInputFrame frame = NetworkInputFrame.Read(reader);
            packet = new NetworkTickPacket
            {
                tick = frame.tick,
                stateChecksum = 0UL,
                player1Frame = frame,
            };
            return true;
        }

        if (packetType == PacketTypeServerTick)
        {
            int headerSize = 1 + 8 + 8;

            if (length < headerSize + NetworkInputFrame.SerializedSize * 2)
            {
                return false;
            }

            long tick = reader.ReadInt64();
            ulong checksum = reader.ReadUInt64();
            NetworkInputFrame player1Frame = NetworkInputFrame.Read(reader);
            NetworkInputFrame player2Frame = NetworkInputFrame.Read(reader);
            List<VesselCompactState> correctionStates = ReadCorrectionStates(reader);

            packet = new NetworkTickPacket
            {
                tick = tick,
                stateChecksum = checksum,
                player1Frame = player1Frame,
                player2Frame = player2Frame,
                correctionStates = correctionStates,
            };
            return true;
        }

        if (packetType == PacketTypeWorldSnapshot)
        {
            if (length < 5)
            {
                return false;
            }

            int snapshotLength = reader.ReadInt32();

            if (snapshotLength < 0 || length < 5 + snapshotLength)
            {
                return false;
            }

            byte[] snapshotData = reader.ReadBytes(snapshotLength);
            packet = new NetworkTickPacket
            {
                tick = 0L,
                stateChecksum = 0UL,
                snapshotData = snapshotData,
            };
            return true;
        }

        return false;
    }
}
