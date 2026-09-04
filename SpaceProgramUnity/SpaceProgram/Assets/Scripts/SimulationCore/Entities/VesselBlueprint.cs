using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// One placed part in a VAB blueprint (.craft / .json).
/// Tree links use <see cref="blockId"/> and <see cref="parentBlockId"/>.
/// </summary>
public class BlockSnapshot
{
    public string blockId;
    public string partTemplateId;
    public string parentBlockId;
    public SimVector3 localPosition;
    public SimQuaternion localRotation;
    public int attachedToNodeIndex;
    public bool isDisjointed;
}

/// <summary>
/// Serializable vessel layout for the 3D VAB editor.
/// Unity-free DTO — file I/O lives in <see cref="VabFileSystem"/>.
/// </summary>
public class VesselBlueprint
{
    public const int SchemaVersion = 1;

    public int schemaVersion = SchemaVersion;
    public string vesselName;
    public List<BlockSnapshot> blocks = new List<BlockSnapshot>();

    public static string SerializeToJson(VesselBlueprint blueprint, bool prettyPrint = true)
    {
        if (blueprint == null)
        {
            throw new ArgumentNullException(nameof(blueprint));
        }

        if (string.IsNullOrWhiteSpace(blueprint.vesselName))
        {
            throw new ArgumentException("Vessel name is required.", nameof(blueprint));
        }

        StringBuilder builder = new StringBuilder();
        WriteObjectStart(builder, prettyPrint, 0);
        AppendInt(builder, "schemaVersion", blueprint.schemaVersion, prettyPrint, 1);
        builder.Append(',');
        AppendNewLine(builder, prettyPrint, 1);
        AppendString(builder, "vesselName", blueprint.vesselName, prettyPrint, 1);
        builder.Append(',');
        AppendNewLine(builder, prettyPrint, 1);
        AppendBlocks(builder, blueprint.blocks, prettyPrint, 1);
        WriteObjectEnd(builder, prettyPrint, 0);
        return builder.ToString();
    }

    public static VesselBlueprint DeserializeFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Blueprint JSON is empty.", nameof(json));
        }

        int index = 0;
        SkipWhitespace(json, ref index);
        Expect(json, ref index, '{');

        VesselBlueprint blueprint = new VesselBlueprint();

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
                case "schemaVersion":
                    blueprint.schemaVersion = (int)ReadNumber(json, ref index);
                    break;
                case "vesselName":
                    blueprint.vesselName = ReadString(json, ref index);
                    break;
                case "blocks":
                    blueprint.blocks = ParseBlockSnapshotArray(json, ref index);
                    break;
                default:
                    SkipValue(json, ref index);
                    break;
            }

            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ',')
            {
                index++;
            }
        }

        if (blueprint.blocks == null)
        {
            blueprint.blocks = new List<BlockSnapshot>();
        }

        return blueprint;
    }

    static void AppendBlocks(
        StringBuilder builder,
        List<BlockSnapshot> blocks,
        bool prettyPrint,
        int indent
    )
    {
        builder.Append('"');
        builder.Append("blocks");
        builder.Append("\":");
        WriteArrayStart(builder, prettyPrint, indent);

        if (blocks != null)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                    AppendNewLine(builder, prettyPrint, indent + 1);
                }

                AppendBlockSnapshot(builder, blocks[i], prettyPrint, indent + 1);
            }
        }

        WriteArrayEnd(builder, prettyPrint, indent);
    }

    static void AppendBlockSnapshot(
        StringBuilder builder,
        BlockSnapshot block,
        bool prettyPrint,
        int indent
    )
    {
        if (block == null)
        {
            builder.Append("null");
            return;
        }

        WriteObjectStart(builder, prettyPrint, indent);
        AppendString(builder, "blockId", block.blockId ?? string.Empty, prettyPrint, indent + 1);
        builder.Append(',');
        AppendNewLine(builder, prettyPrint, indent + 1);
        AppendString(builder, "partTemplateId", block.partTemplateId ?? string.Empty, prettyPrint, indent + 1);
        builder.Append(',');
        AppendNewLine(builder, prettyPrint, indent + 1);
        AppendString(builder, "parentBlockId", block.parentBlockId ?? string.Empty, prettyPrint, indent + 1);
        builder.Append(',');
        AppendNewLine(builder, prettyPrint, indent + 1);
        AppendVector3(builder, "localPosition", block.localPosition, prettyPrint, indent + 1);
        builder.Append(',');
        AppendNewLine(builder, prettyPrint, indent + 1);
        AppendQuaternion(builder, "localRotation", block.localRotation, prettyPrint, indent + 1);
        builder.Append(',');
        AppendNewLine(builder, prettyPrint, indent + 1);
        AppendInt(builder, "attachedToNodeIndex", block.attachedToNodeIndex, prettyPrint, indent + 1);
        WriteObjectEnd(builder, prettyPrint, indent);
    }

    static List<BlockSnapshot> ParseBlockSnapshotArray(string json, ref int index)
    {
        List<BlockSnapshot> blocks = new List<BlockSnapshot>();
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

            blocks.Add(ParseBlockSnapshot(json, ref index));
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ',')
            {
                index++;
            }
        }

        return blocks;
    }

    static BlockSnapshot ParseBlockSnapshot(string json, ref int index)
    {
        BlockSnapshot block = new BlockSnapshot();
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
                case "blockId":
                    block.blockId = ReadString(json, ref index);
                    break;
                case "partTemplateId":
                    block.partTemplateId = ReadString(json, ref index);
                    break;
                case "parentBlockId":
                    block.parentBlockId = ReadString(json, ref index);
                    break;
                case "localPosition":
                    block.localPosition = ReadVector3(json, ref index);
                    break;
                case "localRotation":
                    block.localRotation = ReadQuaternion(json, ref index);
                    break;
                case "attachedToNodeIndex":
                    block.attachedToNodeIndex = (int)ReadNumber(json, ref index);
                    break;
                default:
                    SkipValue(json, ref index);
                    break;
            }

            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ',')
            {
                index++;
            }
        }

        return block;
    }

    static void AppendString(
        StringBuilder builder,
        string key,
        string value,
        bool prettyPrint,
        int indent
    )
    {
        AppendIndent(builder, prettyPrint, indent);
        builder.Append('"');
        builder.Append(key);
        builder.Append("\":");
        AppendSpace(builder, prettyPrint);
        builder.Append('"');
        builder.Append(Escape(value ?? string.Empty));
        builder.Append('"');
    }

    static void AppendInt(
        StringBuilder builder,
        string key,
        int value,
        bool prettyPrint,
        int indent
    )
    {
        AppendIndent(builder, prettyPrint, indent);
        builder.Append('"');
        builder.Append(key);
        builder.Append("\":");
        AppendSpace(builder, prettyPrint);
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    static void AppendVector3(
        StringBuilder builder,
        string key,
        SimVector3 value,
        bool prettyPrint,
        int indent
    )
    {
        AppendIndent(builder, prettyPrint, indent);
        builder.Append('"');
        builder.Append(key);
        builder.Append("\":");
        AppendSpace(builder, prettyPrint);
        builder.Append('[');
        builder.Append(value.x.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        AppendSpace(builder, prettyPrint);
        builder.Append(value.y.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        AppendSpace(builder, prettyPrint);
        builder.Append(value.z.ToString(CultureInfo.InvariantCulture));
        builder.Append(']');
    }

    static void AppendQuaternion(
        StringBuilder builder,
        string key,
        SimQuaternion value,
        bool prettyPrint,
        int indent
    )
    {
        AppendIndent(builder, prettyPrint, indent);
        builder.Append('"');
        builder.Append(key);
        builder.Append("\":");
        AppendSpace(builder, prettyPrint);
        builder.Append('[');
        builder.Append(value.w.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        AppendSpace(builder, prettyPrint);
        builder.Append(value.x.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        AppendSpace(builder, prettyPrint);
        builder.Append(value.y.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        AppendSpace(builder, prettyPrint);
        builder.Append(value.z.ToString(CultureInfo.InvariantCulture));
        builder.Append(']');
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

    static SimQuaternion ReadQuaternion(string json, ref int index)
    {
        SkipWhitespace(json, ref index);

        if (index < json.Length && json[index] == '[')
        {
            index++;
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

        Expect(json, ref index, '{');
        double vw = 1.0;
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
                case "w":
                    vw = component;
                    break;
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

        return new SimQuaternion(vw, vx, vy, vz);
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

        throw new FormatException("Unterminated JSON string in vessel blueprint.");
    }

    static double ReadNumber(string json, ref int index)
    {
        SkipWhitespace(json, ref index);
        int start = index;

        if (index < json.Length && (json[index] == '-' || json[index] == '+'))
        {
            index++;
        }

        while (
            index < json.Length &&
            (
                char.IsDigit(json[index]) ||
                json[index] == '.' ||
                json[index] == 'e' ||
                json[index] == 'E' ||
                json[index] == '-' ||
                json[index] == '+'
            )
        )
        {
            index++;
        }

        string token = json.Substring(start, index - start);

        if (
            !double.TryParse(
                token,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value
            )
        )
        {
            throw new FormatException("Invalid JSON number in vessel blueprint: " + token);
        }

        return value;
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

        if (current == '{' || current == '[')
        {
            char open = current;
            char close = current == '{' ? '}' : ']';
            int depth = 0;

            while (index < json.Length)
            {
                if (json[index] == open)
                {
                    depth++;
                }
                else if (json[index] == close)
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

    static void Expect(string json, ref int index, char expected)
    {
        SkipWhitespace(json, ref index);

        if (index >= json.Length || json[index] != expected)
        {
            throw new FormatException(
                "Expected '" + expected + "' in vessel blueprint JSON."
            );
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

    static void WriteObjectStart(StringBuilder builder, bool prettyPrint, int indent)
    {
        builder.Append('{');
        AppendNewLine(builder, prettyPrint, indent + 1);
    }

    static void WriteObjectEnd(StringBuilder builder, bool prettyPrint, int indent)
    {
        AppendNewLine(builder, prettyPrint, indent);
        AppendIndent(builder, prettyPrint, indent);
        builder.Append('}');
    }

    static void WriteArrayStart(StringBuilder builder, bool prettyPrint, int indent)
    {
        builder.Append('[');
        AppendNewLine(builder, prettyPrint, indent + 1);
    }

    static void WriteArrayEnd(StringBuilder builder, bool prettyPrint, int indent)
    {
        AppendNewLine(builder, prettyPrint, indent);
        AppendIndent(builder, prettyPrint, indent);
        builder.Append(']');
    }

    static void AppendNewLine(StringBuilder builder, bool prettyPrint, int indent)
    {
        if (!prettyPrint)
        {
            return;
        }

        builder.Append('\n');
        AppendIndent(builder, true, indent);
    }

    static void AppendIndent(StringBuilder builder, bool prettyPrint, int indent)
    {
        if (!prettyPrint)
        {
            return;
        }

        for (int i = 0; i < indent; i++)
        {
            builder.Append("  ");
        }
    }

    static void AppendSpace(StringBuilder builder, bool prettyPrint)
    {
        if (prettyPrint)
        {
            builder.Append(' ');
        }
    }
}
