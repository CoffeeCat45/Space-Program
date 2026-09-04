using System;
using System.IO;

struct NetworkInputFrame
{
    public const int Player1VesselId = 1;
    public const int Player2VesselId = 2;

    public long tick;
    public int vesselId;
    public float throttle;
    public bool staging;
    public SimVector3 rcsVector;
    public ShipOrientationMode orientationMode;
    public float pitchInput;
    public float yawInput;
    public float rollInput;
    public bool isPauseRequested;

    public const int SerializedSize = 55;

    public static NetworkInputFrame CreateIdle(int playerSlot = NetworkInputFrame.Player1VesselId, long tick = 0L)
    {
        return NetworkInputDefaults.CreateIdleFrame(playerSlot, tick);
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(tick);
        writer.Write(vesselId);
        writer.Write(throttle);
        writer.Write(staging);
        writer.Write(rcsVector.x);
        writer.Write(rcsVector.y);
        writer.Write(rcsVector.z);
        writer.Write((byte)orientationMode);
        writer.Write(pitchInput);
        writer.Write(yawInput);
        writer.Write(rollInput);
        writer.Write(isPauseRequested);
    }

    public static NetworkInputFrame Read(BinaryReader reader)
    {
        return new NetworkInputFrame
        {
            tick = reader.ReadInt64(),
            vesselId = reader.ReadInt32(),
            throttle = reader.ReadSingle(),
            staging = reader.ReadBoolean(),
            rcsVector = new SimVector3(
                reader.ReadDouble(),
                reader.ReadDouble(),
                reader.ReadDouble()
            ),
            orientationMode = (ShipOrientationMode)reader.ReadByte(),
            pitchInput = reader.ReadSingle(),
            yawInput = reader.ReadSingle(),
            rollInput = reader.ReadSingle(),
            isPauseRequested = reader.ReadBoolean(),
        };
    }

    public byte[] Serialize()
    {
        using MemoryStream stream = new MemoryStream(SerializedSize);
        using BinaryWriter writer = new BinaryWriter(stream);
        Write(writer);
        return stream.ToArray();
    }

    public static NetworkInputFrame Deserialize(byte[] data, int offset, int count)
    {
        using MemoryStream stream = new MemoryStream(data, offset, count);
        using BinaryReader reader = new BinaryReader(stream);
        return Read(reader);
    }
}
