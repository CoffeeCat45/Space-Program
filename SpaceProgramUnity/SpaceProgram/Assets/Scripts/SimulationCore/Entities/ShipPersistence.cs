using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

enum VesselSnapshotType
{
    Player = 0,
    Dockable = 1,
    Debris = 2
}

struct BlockSnapshotState
{
    public int blockId;
    public double fuelMass;
    public bool isDestroyed;
}

struct BodySnapshotData
{
    public string name;
    public SimVector3 position;
    public SimVector3 velocity;
}

class VesselSnapshotData
{
    public int id;
    public string name;
    public VesselSnapshotType type;
    public FlightStatus flightStatus;
    public int targetVesselId;
    public SimVector3 position;
    public SimVector3 velocity;
    public SimQuaternion rotation;
    public SimVector3 angularVelocity;
    public int activeStageIndex;
    public List<StageData> stages;
    public List<BlockSnapshotState> blockStates;
    public string landedOnBodyName;
    public bool isDocked;
    public bool isOnKeplerRails;
    public KeplerRailsState keplerRails;
}

class WorldSnapshot
{
    public long currentTick;
    public double universalTime;
    public int activeVesselId;
    public SimulationScenario scenario;
    public List<BodySnapshotData> bodies = new List<BodySnapshotData>();
    public List<VesselSnapshotData> vessels = new List<VesselSnapshotData>();
}

static class ShipPersistence
{
    const uint SnapshotMagic = 0x4E535053;
    const int SnapshotVersion = 2;
    public static void SaveShip(string path, List<StageData> stages)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        if (stages == null)
        {
            throw new ArgumentNullException(nameof(stages));
        }

        string directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, SerializeStages(stages), Encoding.UTF8);
    }

    public static List<StageData> LoadShip(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Ship configuration file was not found.", path);
        }

        return DeserializeStages(File.ReadAllText(path, Encoding.UTF8));
    }

    public static string SerializeStages(List<StageData> stages)
    {
        bool hasExplicitBlocks = false;

        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i].blocks != null && stages[i].blocks.Count > 0)
            {
                hasExplicitBlocks = true;
                break;
            }
        }

        if (!hasExplicitBlocks)
        {
            return SerializeStageArray(stages);
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("{\"schemaVersion\":2,\"stages\":");
        builder.Append(SerializeStageArray(stages));
        builder.Append('}');
        return builder.ToString();
    }

    static string SerializeStageArray(List<StageData> stages)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append('[');

        for (int i = 0; i < stages.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            StageData stage = stages[i];
            builder.Append('{');
            AppendString(builder, "name", stage.name);
            builder.Append(',');
            AppendNumber(builder, "dryMass", stage.dryMass);
            builder.Append(',');
            AppendNumber(builder, "fuelMass", stage.fuelMass);
            builder.Append(',');
            AppendNumber(builder, "maxFuelMass", stage.maxFuelMass);
            builder.Append(',');
            AppendNumber(builder, "thrust", stage.thrust);
            builder.Append(',');
            AppendNumber(builder, "isp", stage.isp);
            builder.Append(',');
            AppendNumber(builder, "dragCoefficient", stage.dragCoefficient);

            if (stage.blocks != null && stage.blocks.Count > 0)
            {
                builder.Append(',');
                AppendBlocks(builder, stage.blocks);
            }

            builder.Append('}');
        }

        builder.Append(']');
        return builder.ToString();
    }

    static void AppendBlocks(StringBuilder builder, List<ShipBlock> blocks)
    {
        builder.Append("\"blocks\":[");

        for (int i = 0; i < blocks.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            ShipBlock block = blocks[i];
            builder.Append('{');
            AppendString(builder, "id", block.id);
            builder.Append(',');
            AppendString(builder, "partTemplateId", block.partTemplateId);
            builder.Append(',');
            AppendString(builder, "blockType", block.blockType.ToString());
            builder.Append(',');
            AppendVector3(builder, "localOffset", block.localOffset);
            builder.Append(',');
            AppendVector3(builder, "dimensions", block.dimensions);
            builder.Append(',');
            AppendNumber(builder, "dryMass", block.dryMass);
            builder.Append(',');
            AppendNumber(builder, "fuelMass", block.fuelMass);
            builder.Append(',');
            AppendNumber(builder, "fuelCapacity", block.fuelCapacity);
            builder.Append(',');
            AppendBool(builder, "isFuelTank", block.isFuelTank);
            builder.Append(',');
            AppendBool(builder, "isEngine", block.isEngine);
            builder.Append(',');
            AppendBool(builder, "isDockingPort", block.isDockingPort);
            builder.Append(',');
            AppendVector3(builder, "thrustDirection", block.thrustDirection);
            builder.Append(',');
            AppendVector3(builder, "thrustLocalOffset", block.thrustLocalOffset);
            builder.Append(',');
            AppendNumber(builder, "maxThrustNewtons", block.maxThrustNewtons);
            builder.Append(',');
            AppendNumber(builder, "ispSeconds", block.ispSeconds);
            builder.Append(',');
            AppendNumber(builder, "rcsThrust", block.rcsThrust);
            builder.Append(',');
            AppendNumber(builder, "blockId", block.blockId);
            builder.Append(',');
            AppendNumber(builder, "structuralStrength", block.structuralStrength);

            if (block.hasVabAttach)
            {
                builder.Append(',');
                AppendString(builder, "parentBlockId", block.parentBlockId);
                builder.Append(',');
                AppendBool(builder, "hasVabAttach", true);
                builder.Append(',');
                AppendVector3(builder, "attachLocalPosition", block.attachLocalPosition);
                builder.Append(',');
                AppendQuaternion(builder, "attachLocalRotation", block.attachLocalRotation);
                builder.Append(',');
                AppendQuaternion(builder, "localRotation", block.localRotation);
            }

            builder.Append('}');
        }

        builder.Append(']');
    }

    static void AppendVector3(StringBuilder builder, string key, SimVector3 value)
    {
        builder.Append('"');
        builder.Append(key);
        builder.Append("\":[");
        builder.Append(value.x.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(value.y.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(value.z.ToString(CultureInfo.InvariantCulture));
        builder.Append(']');
    }

    static void AppendBool(StringBuilder builder, string key, bool value)
    {
        builder.Append('"');
        builder.Append(key);
        builder.Append("\":");
        builder.Append(value ? "true" : "false");
    }

    public static List<StageData> DeserializeStages(string json)
    {
        List<StageData> stages = new List<StageData>();

        if (string.IsNullOrWhiteSpace(json))
        {
            return stages;
        }

        int index = 0;
        SkipWhitespace(json, ref index);

        if (index < json.Length && json[index] == '{')
        {
            stages = ParseShipDocument(json, ref index);
        }
        else
        {
            stages = ParseStageArray(json, ref index);
        }

        StageData.PrepareStages(stages);
        return stages;
    }

    static List<StageData> ParseShipDocument(string json, ref int index)
    {
        List<StageData> stages = new List<StageData>();
        SkipWhitespace(json, ref index);
        Expect(json, ref index, '{');

        while (index < json.Length)
        {
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == '}')
            {
                index++;
                break;
            }

            string key = ReadString(json, ref index);
            SkipWhitespace(json, ref index);
            Expect(json, ref index, ':');
            SkipWhitespace(json, ref index);

            if (key == "stages")
            {
                stages = ParseStageArray(json, ref index);
            }
            else
            {
                SkipValue(json, ref index);
            }

            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ',')
            {
                index++;
            }
        }

        return stages;
    }

    static List<StageData> ParseStageArray(string json, ref int index)
    {
        List<StageData> stages = new List<StageData>();
        SkipWhitespace(json, ref index);
        Expect(json, ref index, '[');

        while (index < json.Length)
        {
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ']')
            {
                index++;
                break;
            }

            stages.Add(ParseStage(json, ref index));
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ',')
            {
                index++;
            }
        }

        return stages;
    }

    static StageData ParseStage(string json, ref int index)
    {
        StageData stage = new StageData();
        SkipWhitespace(json, ref index);
        Expect(json, ref index, '{');

        while (index < json.Length)
        {
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == '}')
            {
                index++;
                break;
            }

            string key = ReadString(json, ref index);
            SkipWhitespace(json, ref index);
            Expect(json, ref index, ':');
            SkipWhitespace(json, ref index);

            switch (key)
            {
                case "name":
                    stage.name = ReadString(json, ref index);
                    break;
                case "blocks":
                    stage.blocks = ParseBlockArray(json, ref index);
                    break;
                default:
                    double value = ReadNumber(json, ref index);

                    switch (key)
                    {
                        case "dryMass":
                            stage.dryMass = value;
                            break;
                        case "fuelMass":
                            stage.fuelMass = value;
                            break;
                        case "maxFuelMass":
                            stage.maxFuelMass = value;
                            break;
                        case "thrust":
                            stage.thrust = value;
                            break;
                        case "isp":
                            stage.isp = value;
                            break;
                        case "dragCoefficient":
                            stage.dragCoefficient = value;
                            break;
                    }

                    break;
            }

            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ',')
            {
                index++;
            }
        }

        if (stage.maxFuelMass <= 0.0)
        {
            stage.maxFuelMass = stage.fuelMass;
        }

        if (stage.blocks != null && stage.blocks.Count > 0)
        {
            stage.suppressLegacyPrimitiveBlocks = true;
        }

        return stage;
    }

    static List<ShipBlock> ParseBlockArray(string json, ref int index)
    {
        List<ShipBlock> blocks = new List<ShipBlock>();
        SkipWhitespace(json, ref index);
        Expect(json, ref index, '[');

        while (index < json.Length)
        {
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ']')
            {
                index++;
                break;
            }

            blocks.Add(ParseBlock(json, ref index));
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ',')
            {
                index++;
            }
        }

        return blocks;
    }

    static ShipBlock ParseBlock(string json, ref int index)
    {
        ShipBlock block = new ShipBlock();
        SkipWhitespace(json, ref index);
        Expect(json, ref index, '{');

        while (index < json.Length)
        {
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == '}')
            {
                index++;
                break;
            }

            string key = ReadString(json, ref index);
            SkipWhitespace(json, ref index);
            Expect(json, ref index, ':');
            SkipWhitespace(json, ref index);

            switch (key)
            {
                case "id":
                    block.id = ReadString(json, ref index);
                    break;
                case "partTemplateId":
                    block.partTemplateId = ReadString(json, ref index);
                    break;
                case "parentBlockId":
                    block.parentBlockId = ReadString(json, ref index);
                    break;
                case "hasVabAttach":
                    block.hasVabAttach = ReadBoolean(json, ref index);
                    break;
                case "attachLocalPosition":
                    block.attachLocalPosition = ReadVector3(json, ref index);
                    break;
                case "attachLocalRotation":
                    block.attachLocalRotation = ReadJsonQuaternion(json, ref index);
                    break;
                case "localRotation":
                    block.localRotation = ReadJsonQuaternion(json, ref index);
                    break;
                case "blockType":
                    block.blockType = ParseBlockType(ReadString(json, ref index));
                    break;
                case "localOffset":
                    block.localOffset = ReadVector3(json, ref index);
                    break;
                case "dimensions":
                    block.dimensions = ReadVector3(json, ref index);
                    break;
                case "thrustDirection":
                    block.thrustDirection = ReadVector3(json, ref index);
                    break;
                case "thrustLocalOffset":
                    block.thrustLocalOffset = ReadVector3(json, ref index);
                    break;
                case "isFuelTank":
                    block.isFuelTank = ReadBoolean(json, ref index);
                    break;
                case "isEngine":
                    block.isEngine = ReadBoolean(json, ref index);
                    break;
                case "isDockingPort":
                    block.isDockingPort = ReadBoolean(json, ref index);
                    break;
                default:
                    double value = ReadNumber(json, ref index);

                    switch (key)
                    {
                        case "dryMass":
                            block.dryMass = value;
                            break;
                        case "fuelMass":
                            block.fuelMass = value;
                            break;
                        case "fuelCapacity":
                            block.fuelCapacity = value;
                            break;
                        case "rcsThrust":
                            block.rcsThrust = value;
                            break;
                        case "maxThrustNewtons":
                            block.maxThrustNewtons = value;
                            break;
                        case "ispSeconds":
                            block.ispSeconds = value;
                            break;
                        case "blockId":
                            block.blockId = (int)value;
                            break;
                        case "structuralStrength":
                            block.structuralStrength = value;
                            break;
                    }

                    break;
            }

            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ',')
            {
                index++;
            }
        }

        if (block.fuelCapacity <= 0.0 && block.isFuelTank)
        {
            block.fuelCapacity = block.fuelMass;
        }

        if (block.blockType == ShipBlockType.DockingPort)
        {
            block.isDockingPort = true;
        }

        return block;
    }

    static ShipBlockType ParseBlockType(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return ShipBlockType.Adapter;
        }

        if (Enum.TryParse(value, true, out ShipBlockType parsed))
        {
            return parsed;
        }

        return ShipBlockType.Adapter;
    }

    static SimVector3 ReadVector3(string json, ref int index)
    {
        SkipWhitespace(json, ref index);

        if (index < json.Length && json[index] == '[')
        {
            index++;
            double x = ReadNumber(json, ref index);
            SkipWhitespace(json, ref index);
            Expect(json, ref index, ',');
            double y = ReadNumber(json, ref index);
            SkipWhitespace(json, ref index);
            Expect(json, ref index, ',');
            double z = ReadNumber(json, ref index);
            SkipWhitespace(json, ref index);
            Expect(json, ref index, ']');
            return new SimVector3(x, y, z);
        }

        Expect(json, ref index, '{');
        double vx = 0.0;
        double vy = 0.0;
        double vz = 0.0;

        while (index < json.Length)
        {
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == '}')
            {
                index++;
                break;
            }

            string key = ReadString(json, ref index);
            SkipWhitespace(json, ref index);
            Expect(json, ref index, ':');
            double component = ReadNumber(json, ref index);

            switch (key)
            {
                case "x":
                    vx = component;
                    break;
                case "y":
                    vy = component;
                    break;
                case "z":
                    vz = component;
                    break;
            }

            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ',')
            {
                index++;
            }
        }

        return new SimVector3(vx, vy, vz);
    }

    static bool ReadBoolean(string json, ref int index)
    {
        SkipWhitespace(json, ref index);

        if (
            index + 4 <= json.Length &&
            string.Compare(json, index, "true", 0, 4, true) == 0
        )
        {
            index += 4;
            return true;
        }

        if (
            index + 5 <= json.Length &&
            string.Compare(json, index, "false", 0, 5, true) == 0
        )
        {
            index += 5;
            return false;
        }

        throw new FormatException("Invalid JSON boolean.");
    }

    static void SkipValue(string json, ref int index)
    {
        SkipWhitespace(json, ref index);

        if (index >= json.Length)
        {
            return;
        }

        char current = json[index];

        if (current == '"')
        {
            ReadString(json, ref index);
            return;
        }

        if (current == '{')
        {
            int depth = 0;

            while (index < json.Length)
            {
                if (json[index] == '{')
                {
                    depth++;
                }
                else if (json[index] == '}')
                {
                    depth--;

                    if (depth == 0)
                    {
                        index++;
                        return;
                    }
                }

                index++;
            }

            return;
        }

        if (current == '[')
        {
            int depth = 0;

            while (index < json.Length)
            {
                if (json[index] == '[')
                {
                    depth++;
                }
                else if (json[index] == ']')
                {
                    depth--;

                    if (depth == 0)
                    {
                        index++;
                        return;
                    }
                }

                index++;
            }

            return;
        }

        ReadNumber(json, ref index);
    }

    static void AppendString(StringBuilder builder, string key, string value)
    {
        builder.Append('"');
        builder.Append(key);
        builder.Append("\":");
        builder.Append('"');
        builder.Append(Escape(value ?? string.Empty));
        builder.Append('"');
    }

    static void AppendNumber(StringBuilder builder, string key, double value)
    {
        builder.Append('"');
        builder.Append(key);
        builder.Append("\":");
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    static string ReadString(string json, ref int index)
    {
        SkipWhitespace(json, ref index);
        Expect(json, ref index, '"');

        StringBuilder builder = new StringBuilder();

        while (index < json.Length)
        {
            char current = json[index++];

            if (current == '"')
            {
                return builder.ToString();
            }

            if (current == '\\' && index < json.Length)
            {
                char escaped = json[index++];

                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escaped);
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    default:
                        builder.Append(escaped);
                        break;
                }

                continue;
            }

            builder.Append(current);
        }

        throw new FormatException("Unterminated JSON string.");
    }

    static double ReadNumber(string json, ref int index)
    {
        SkipWhitespace(json, ref index);
        int start = index;

        if (index < json.Length && (json[index] == '-' || json[index] == '+'))
        {
            index++;
        }

        while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '.' || json[index] == 'e' || json[index] == 'E' || json[index] == '-' || json[index] == '+'))
        {
            index++;
        }

        string token = json.Substring(start, index - start);

        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            throw new FormatException("Invalid JSON number: " + token);
        }

        return value;
    }

    static void Expect(string json, ref int index, char expected)
    {
        SkipWhitespace(json, ref index);

        if (index >= json.Length || json[index] != expected)
        {
            throw new FormatException("Expected '" + expected + "' in ship JSON.");
        }

        index++;
    }

    static void SkipWhitespace(string json, ref int index)
    {
        while (index < json.Length && char.IsWhiteSpace(json[index]))
        {
            index++;
        }
    }

    public static byte[] SerializeToBinary(WorldSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        using MemoryStream stream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
        WriteSnapshot(writer, snapshot);
        return stream.ToArray();
    }

    public static WorldSnapshot DeserializeFromBinary(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Snapshot data is empty.", nameof(data));
        }

        using MemoryStream stream = new MemoryStream(data, false);
        using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
        return ReadSnapshot(reader);
    }

    public static string SerializeToDebugJson(WorldSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        StringBuilder builder = new StringBuilder();
        builder.Append('{');
        AppendNumber(builder, "currentTick", snapshot.currentTick);
        builder.Append(',');
        AppendNumber(builder, "universalTime", snapshot.universalTime);
        builder.Append(',');
        AppendNumber(builder, "activeVesselId", snapshot.activeVesselId);
        builder.Append(",\"bodies\":");
        AppendBodySnapshotArray(builder, snapshot.bodies);
        builder.Append(",\"vessels\":");
        AppendVesselSnapshotArray(builder, snapshot.vessels);
        builder.Append('}');
        return builder.ToString();
    }

    static void WriteSnapshot(BinaryWriter writer, WorldSnapshot snapshot)
    {
        writer.Write(SnapshotMagic);
        writer.Write(SnapshotVersion);
        writer.Write(snapshot.currentTick);
        writer.Write(snapshot.universalTime);
        writer.Write(snapshot.activeVesselId);
        writer.Write((int)snapshot.scenario);

        int bodyCount = snapshot.bodies == null ? 0 : snapshot.bodies.Count;
        writer.Write(bodyCount);

        for (int i = 0; i < bodyCount; i++)
        {
            WriteBodySnapshot(writer, snapshot.bodies[i]);
        }

        int vesselCount = snapshot.vessels == null ? 0 : snapshot.vessels.Count;
        writer.Write(vesselCount);

        for (int i = 0; i < vesselCount; i++)
        {
            WriteVesselSnapshot(writer, snapshot.vessels[i]);
        }
    }

    static WorldSnapshot ReadSnapshot(BinaryReader reader)
    {
        uint magic = reader.ReadUInt32();

        if (magic != SnapshotMagic)
        {
            throw new FormatException("Invalid world snapshot magic.");
        }

        int version = reader.ReadInt32();

        if (version != SnapshotVersion)
        {
            throw new FormatException("Unsupported world snapshot version: " + version);
        }

        WorldSnapshot snapshot = new WorldSnapshot
        {
            currentTick = reader.ReadInt64(),
            universalTime = reader.ReadDouble(),
            activeVesselId = reader.ReadInt32(),
            scenario = version >= 2 ?
                (SimulationScenario)reader.ReadInt32() :
                SimulationScenario.PlanetSurfaceStart,
        };

        int bodyCount = reader.ReadInt32();

        for (int i = 0; i < bodyCount; i++)
        {
            snapshot.bodies.Add(ReadBodySnapshot(reader));
        }

        int vesselCount = reader.ReadInt32();

        for (int i = 0; i < vesselCount; i++)
        {
            snapshot.vessels.Add(ReadVesselSnapshot(reader));
        }

        return snapshot;
    }

    static void WriteBodySnapshot(BinaryWriter writer, BodySnapshotData body)
    {
        WriteString(writer, body.name ?? string.Empty);
        WriteVector3(writer, body.position);
        WriteVector3(writer, body.velocity);
    }

    static BodySnapshotData ReadBodySnapshot(BinaryReader reader)
    {
        return new BodySnapshotData
        {
            name = ReadString(reader),
            position = ReadVector3(reader),
            velocity = ReadVector3(reader)
        };
    }

    static void WriteVesselSnapshot(BinaryWriter writer, VesselSnapshotData vessel)
    {
        writer.Write(vessel.id);
        WriteString(writer, vessel.name ?? string.Empty);
        writer.Write((int)vessel.type);
        writer.Write((int)vessel.flightStatus);
        writer.Write(vessel.targetVesselId);
        WriteVector3(writer, vessel.position);
        WriteVector3(writer, vessel.velocity);
        WriteQuaternion(writer, vessel.rotation);
        WriteVector3(writer, vessel.angularVelocity);
        writer.Write(vessel.activeStageIndex);
        WriteString(writer, vessel.landedOnBodyName ?? string.Empty);
        writer.Write(vessel.isDocked);
        writer.Write(vessel.isOnKeplerRails);
        WriteKeplerRails(writer, vessel.keplerRails);
        WriteStages(writer, vessel.stages);

        int blockStateCount = vessel.blockStates == null ? 0 : vessel.blockStates.Count;
        writer.Write(blockStateCount);

        for (int i = 0; i < blockStateCount; i++)
        {
            BlockSnapshotState blockState = vessel.blockStates[i];
            writer.Write(blockState.blockId);
            writer.Write(blockState.fuelMass);
            writer.Write(blockState.isDestroyed);
        }
    }

    static VesselSnapshotData ReadVesselSnapshot(BinaryReader reader)
    {
        VesselSnapshotData vessel = new VesselSnapshotData
        {
            id = reader.ReadInt32(),
            name = ReadString(reader),
            type = (VesselSnapshotType)reader.ReadInt32(),
            flightStatus = (FlightStatus)reader.ReadInt32(),
            targetVesselId = reader.ReadInt32(),
            position = ReadVector3(reader),
            velocity = ReadVector3(reader),
            rotation = ReadQuaternion(reader),
            angularVelocity = ReadVector3(reader),
            activeStageIndex = reader.ReadInt32(),
            landedOnBodyName = ReadString(reader),
            isDocked = reader.ReadBoolean(),
            isOnKeplerRails = reader.ReadBoolean(),
            keplerRails = ReadKeplerRails(reader),
            stages = ReadStages(reader)
        };

        int blockStateCount = reader.ReadInt32();
        vessel.blockStates = new List<BlockSnapshotState>(blockStateCount);

        for (int i = 0; i < blockStateCount; i++)
        {
            vessel.blockStates.Add(
                new BlockSnapshotState
                {
                    blockId = reader.ReadInt32(),
                    fuelMass = reader.ReadDouble(),
                    isDestroyed = reader.ReadBoolean()
                }
            );
        }

        return vessel;
    }

    static void WriteStages(BinaryWriter writer, List<StageData> stages)
    {
        int stageCount = stages == null ? 0 : stages.Count;
        writer.Write(stageCount);

        for (int i = 0; i < stageCount; i++)
        {
            WriteStage(writer, stages[i]);
        }
    }

    static List<StageData> ReadStages(BinaryReader reader)
    {
        int stageCount = reader.ReadInt32();
        List<StageData> stages = new List<StageData>(stageCount);

        for (int i = 0; i < stageCount; i++)
        {
            stages.Add(ReadStage(reader));
        }

        StageData.PrepareStages(stages);
        return stages;
    }

    static void WriteStage(BinaryWriter writer, StageData stage)
    {
        WriteString(writer, stage.name ?? string.Empty);
        writer.Write(stage.dryMass);
        writer.Write(stage.fuelMass);
        writer.Write(stage.maxFuelMass);
        writer.Write(stage.thrust);
        writer.Write(stage.isp);
        writer.Write(stage.dragCoefficient);
        WriteVector3(writer, stage.stageMountOffset);

        int blockCount = stage.blocks == null ? 0 : stage.blocks.Count;
        writer.Write(blockCount);

        for (int i = 0; i < blockCount; i++)
        {
            WriteShipBlock(writer, stage.blocks[i]);
        }
    }

    static StageData ReadStage(BinaryReader reader)
    {
        StageData stage = new StageData
        {
            name = ReadString(reader),
            dryMass = reader.ReadDouble(),
            fuelMass = reader.ReadDouble(),
            maxFuelMass = reader.ReadDouble(),
            thrust = reader.ReadDouble(),
            isp = reader.ReadDouble(),
            dragCoefficient = reader.ReadDouble(),
            stageMountOffset = ReadVector3(reader)
        };

        int blockCount = reader.ReadInt32();
        stage.blocks = new List<ShipBlock>(blockCount);

        for (int i = 0; i < blockCount; i++)
        {
            stage.blocks.Add(ReadShipBlock(reader));
        }

        if (stage.maxFuelMass <= 0.0)
        {
            stage.maxFuelMass = stage.fuelMass;
        }

        if (stage.blocks.Count > 0)
        {
            stage.suppressLegacyPrimitiveBlocks = true;
        }

        return stage;
    }

    static void WriteShipBlock(BinaryWriter writer, ShipBlock block)
    {
        writer.Write(block.blockId);
        WriteString(writer, block.id ?? string.Empty);
        writer.Write((int)block.blockType);
        WriteVector3(writer, block.localOffset);
        WriteVector3(writer, block.dimensions);
        writer.Write(block.dryMass);
        writer.Write(block.fuelMass);
        writer.Write(block.fuelCapacity);
        writer.Write(block.isFuelTank);
        writer.Write(block.isEngine);
        writer.Write(block.isDockingPort);
        writer.Write(block.isDestroyed);
        writer.Write(block.structuralStrength);
        WriteVector3(writer, block.thrustDirection);
        writer.Write(block.rcsThrust);
    }

    static ShipBlock ReadShipBlock(BinaryReader reader)
    {
        ShipBlock block = new ShipBlock
        {
            blockId = reader.ReadInt32(),
            id = ReadString(reader),
            blockType = (ShipBlockType)reader.ReadInt32(),
            localOffset = ReadVector3(reader),
            dimensions = ReadVector3(reader),
            dryMass = reader.ReadDouble(),
            fuelMass = reader.ReadDouble(),
            fuelCapacity = reader.ReadDouble(),
            isFuelTank = reader.ReadBoolean(),
            isEngine = reader.ReadBoolean(),
            isDockingPort = reader.ReadBoolean(),
            isDestroyed = reader.ReadBoolean(),
            structuralStrength = reader.ReadDouble(),
            thrustDirection = ReadVector3(reader),
            rcsThrust = reader.ReadDouble()
        };

        if (block.fuelCapacity <= 0.0 && block.isFuelTank)
        {
            block.fuelCapacity = block.fuelMass;
        }

        if (block.blockType == ShipBlockType.DockingPort)
        {
            block.isDockingPort = true;
        }

        return block;
    }

    static void WriteKeplerRails(BinaryWriter writer, KeplerRailsState rails)
    {
        WriteString(writer, rails.primaryBodyName ?? string.Empty);
        writer.Write(rails.epochTime);
        writer.Write(rails.meanAnomalyAtEpoch);
        writer.Write(rails.semiMajorAxis);
        writer.Write(rails.eccentricity);
        writer.Write(rails.semiLatusRectum);
        WriteVector3(writer, rails.periapsisDirection);
        WriteVector3(writer, rails.sideDirection);
        WriteVector3(writer, rails.orbitNormal);
        writer.Write(rails.isValid);
    }

    static KeplerRailsState ReadKeplerRails(BinaryReader reader)
    {
        return new KeplerRailsState
        {
            primaryBodyName = ReadString(reader),
            epochTime = reader.ReadDouble(),
            meanAnomalyAtEpoch = reader.ReadDouble(),
            semiMajorAxis = reader.ReadDouble(),
            eccentricity = reader.ReadDouble(),
            semiLatusRectum = reader.ReadDouble(),
            periapsisDirection = ReadVector3(reader),
            sideDirection = ReadVector3(reader),
            orbitNormal = ReadVector3(reader),
            isValid = reader.ReadBoolean()
        };
    }

    static void WriteVector3(BinaryWriter writer, SimVector3 value)
    {
        writer.Write(value.x);
        writer.Write(value.y);
        writer.Write(value.z);
    }

    static SimVector3 ReadVector3(BinaryReader reader)
    {
        return new SimVector3(
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadDouble()
        );
    }

    static void WriteQuaternion(BinaryWriter writer, SimQuaternion value)
    {
        writer.Write(value.w);
        writer.Write(value.x);
        writer.Write(value.y);
        writer.Write(value.z);
    }

    static SimQuaternion ReadQuaternion(BinaryReader reader)
    {
        return new SimQuaternion(
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadDouble()
        );
    }

    static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();

        if (length < 0)
        {
            throw new FormatException("Invalid snapshot string length.");
        }

        if (length == 0)
        {
            return string.Empty;
        }

        byte[] bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    static void AppendBodySnapshotArray(
        StringBuilder builder,
        List<BodySnapshotData> bodies
    )
    {
        builder.Append('[');

        if (bodies != null)
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                BodySnapshotData body = bodies[i];
                builder.Append('{');
                AppendString(builder, "name", body.name);
                builder.Append(',');
                AppendVector3(builder, "position", body.position);
                builder.Append(',');
                AppendVector3(builder, "velocity", body.velocity);
                builder.Append('}');
            }
        }

        builder.Append(']');
    }

    static void AppendVesselSnapshotArray(
        StringBuilder builder,
        List<VesselSnapshotData> vessels
    )
    {
        builder.Append('[');

        if (vessels != null)
        {
            for (int i = 0; i < vessels.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                AppendVesselSnapshot(builder, vessels[i]);
            }
        }

        builder.Append(']');
    }

    static void AppendVesselSnapshot(StringBuilder builder, VesselSnapshotData vessel)
    {
        builder.Append('{');
        AppendNumber(builder, "id", vessel.id);
        builder.Append(',');
        AppendString(builder, "name", vessel.name);
        builder.Append(',');
        AppendString(builder, "type", vessel.type.ToString());
        builder.Append(',');
        AppendString(builder, "flightStatus", vessel.flightStatus.ToString());
        builder.Append(',');
        AppendNumber(builder, "targetVesselId", vessel.targetVesselId);
        builder.Append(',');
        AppendVector3(builder, "position", vessel.position);
        builder.Append(',');
        AppendVector3(builder, "velocity", vessel.velocity);
        builder.Append(',');
        AppendQuaternion(builder, "rotation", vessel.rotation);
        builder.Append(',');
        AppendVector3(builder, "angularVelocity", vessel.angularVelocity);
        builder.Append(',');
        AppendNumber(builder, "activeStageIndex", vessel.activeStageIndex);
        builder.Append(',');
        AppendString(builder, "landedOnBodyName", vessel.landedOnBodyName);
        builder.Append(',');
        AppendBool(builder, "isDocked", vessel.isDocked);
        builder.Append(',');
        AppendBool(builder, "isOnKeplerRails", vessel.isOnKeplerRails);
        builder.Append(",\"stages\":");
        builder.Append(SerializeStages(vessel.stages ?? new List<StageData>()));
        builder.Append(",\"blockStates\":");
        AppendBlockStateArray(builder, vessel.blockStates);
        builder.Append('}');
    }

    static void AppendQuaternion(
        StringBuilder builder,
        string key,
        SimQuaternion value
    )
    {
        builder.Append('"');
        builder.Append(key);
        builder.Append("\":[");
        builder.Append(value.w.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(value.x.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(value.y.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(value.z.ToString(CultureInfo.InvariantCulture));
        builder.Append(']');
    }

    static SimQuaternion ReadJsonQuaternion(string json, ref int index)
    {
        SkipWhitespace(json, ref index);
        Expect(json, ref index, '[');
        double w = ReadNumber(json, ref index);
        SkipWhitespace(json, ref index);
        Expect(json, ref index, ',');
        double x = ReadNumber(json, ref index);
        SkipWhitespace(json, ref index);
        Expect(json, ref index, ',');
        double y = ReadNumber(json, ref index);
        SkipWhitespace(json, ref index);
        Expect(json, ref index, ',');
        double z = ReadNumber(json, ref index);
        SkipWhitespace(json, ref index);
        Expect(json, ref index, ']');
        return new SimQuaternion(w, x, y, z);
    }

    static void AppendBlockStateArray(
        StringBuilder builder,
        List<BlockSnapshotState> blockStates
    )
    {
        builder.Append('[');

        if (blockStates != null)
        {
            for (int i = 0; i < blockStates.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                BlockSnapshotState blockState = blockStates[i];
                builder.Append('{');
                AppendNumber(builder, "blockId", blockState.blockId);
                builder.Append(',');
                AppendNumber(builder, "fuelMass", blockState.fuelMass);
                builder.Append(',');
                AppendBool(builder, "isDestroyed", blockState.isDestroyed);
                builder.Append('}');
            }
        }

        builder.Append(']');
    }
}
