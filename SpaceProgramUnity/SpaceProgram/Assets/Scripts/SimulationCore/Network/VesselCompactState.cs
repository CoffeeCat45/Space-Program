using System.IO;

struct VesselCompactState
{
    public int id;
    public SimVector3 pos;
    public SimVector3 vel;

    public const int SerializedSize = 4 + 6 * 8;

    public void Write(BinaryWriter writer)
    {
        writer.Write(id);
        writer.Write(pos.x);
        writer.Write(pos.y);
        writer.Write(pos.z);
        writer.Write(vel.x);
        writer.Write(vel.y);
        writer.Write(vel.z);
    }

    public static VesselCompactState Read(BinaryReader reader)
    {
        return new VesselCompactState
        {
            id = reader.ReadInt32(),
            pos = new SimVector3(
                reader.ReadDouble(),
                reader.ReadDouble(),
                reader.ReadDouble()
            ),
            vel = new SimVector3(
                reader.ReadDouble(),
                reader.ReadDouble(),
                reader.ReadDouble()
            ),
        };
    }
}
