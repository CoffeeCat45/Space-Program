using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

/// <summary>
/// Sidecar metadata for a VAB part prefab: <c>{partId}.partdef.json</c>.
/// Unity-free JSON — written by Editor pipeline, read by <see cref="PartCatalog"/>.
/// </summary>
public class PartDefinitionData
{
    public string id;
    public string displayName;
    public string category;
    public float dryMass;
    public float fuelCapacity;
    public float maxThrustVac;
    public float ispVacuum;
    public List<AttachmentNode> nodes = new List<AttachmentNode>();
}

public static class PartDefinitionFile
{
    const string Extension = ".partdef.json";

    public static string GetSidecarPathForPrefab(string prefabAssetPath)
    {
        if (string.IsNullOrWhiteSpace(prefabAssetPath))
        {
            return string.Empty;
        }

        string directory = Path.GetDirectoryName(prefabAssetPath) ?? string.Empty;
        string partId = Path.GetFileNameWithoutExtension(prefabAssetPath);
        return Path.Combine(directory, partId + Extension).Replace('\\', '/');
    }

    public static bool TryLoad(string assetPath, out PartDefinitionData data)
    {
        data = null;

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        string fullPath = ResolveFullPath(assetPath);

        if (!File.Exists(fullPath))
        {
            return false;
        }

        string json = File.ReadAllText(fullPath, Encoding.UTF8);
        data = DeserializeFromJson(json);
        return data != null && !string.IsNullOrWhiteSpace(data.id);
    }

    public static void Save(string assetPath, PartDefinitionData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (string.IsNullOrWhiteSpace(data.id))
        {
            throw new ArgumentException("Part id is required.", nameof(data));
        }

        string fullPath = ResolveFullPath(assetPath);
        string directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = SerializeToJson(data, prettyPrint: true);
        File.WriteAllText(fullPath, json, Encoding.UTF8);
    }

    public static PartDefinitionData DeserializeFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        int index = 0;
        SkipWhitespace(json, ref index);
        Expect(json, ref index, '{');

        PartDefinitionData data = new PartDefinitionData();

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
                    data.id = ReadString(json, ref index);
                    break;
                case "displayName":
                    data.displayName = ReadString(json, ref index);
                    break;
                case "category":
                    data.category = ReadString(json, ref index);
                    break;
                case "dryMass":
                    data.dryMass = (float)ReadNumber(json, ref index);
                    break;
                case "fuelCapacity":
                    data.fuelCapacity = (float)ReadNumber(json, ref index);
                    break;
                case "maxThrustVac":
                    data.maxThrustVac = (float)ReadNumber(json, ref index);
                    break;
                case "ispVacuum":
                    data.ispVacuum = (float)ReadNumber(json, ref index);
                    break;
                case "nodes":
                    data.nodes = ParseNodeArray(json, ref index);
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

        if (data.nodes == null)
        {
            data.nodes = new List<AttachmentNode>();
        }

        return data;
    }

    public static string SerializeToJson(PartDefinitionData data, bool prettyPrint = true)
    {
        StringBuilder builder = new StringBuilder();
        WriteObjectStart(builder, prettyPrint, 0);
        AppendString(builder, "id", data.id ?? string.Empty, prettyPrint, 1);
        builder.Append(',');
        AppendNewLine(builder, prettyPrint, 1);
        AppendString(builder, "displayName", data.displayName ?? string.Empty, prettyPrint, 1);
        builder.Append(',');
        AppendNewLine(builder, prettyPrint, 1);
        AppendString(builder, "category", data.category ?? string.Empty, prettyPrint, 1);
        builder.Append(',');
        AppendNewLine(builder, prettyPrint, 1);
        AppendFloat(builder, "dryMass", data.dryMass, prettyPrint, 1);
        builder.Append(',');
        AppendNewLine(builder, prettyPrint, 1);
        AppendFloat(builder, "fuelCapacity", data.fuelCapacity, prettyPrint, 1);

        if (data.maxThrustVac > 0f)
        {
            builder.Append(',');
            AppendNewLine(builder, prettyPrint, 1);
            AppendFloat(builder, "maxThrustVac", data.maxThrustVac, prettyPrint, 1);
        }

        if (data.ispVacuum > 0f)
        {
            builder.Append(',');
            AppendNewLine(builder, prettyPrint, 1);
            AppendFloat(builder, "ispVacuum", data.ispVacuum, prettyPrint, 1);
        }

        builder.Append(',');
        AppendNewLine(builder, prettyPrint, 1);
        AppendNodes(builder, data.nodes, prettyPrint, 1);
        WriteObjectEnd(builder, prettyPrint, 0);
        return builder.ToString();
    }

    public static PartTemplate ToTemplate(PartDefinitionData data, string prefabAssetPath)
    {
        if (data == null)
        {
            return null;
        }

        return new PartTemplate
        {
            id = data.id,
            displayName = string.IsNullOrWhiteSpace(data.displayName) ? data.id : data.displayName,
            category = string.IsNullOrWhiteSpace(data.category) ?
                PartDatabase.CategoryMisc :
                data.category,
            dryMass = data.dryMass,
            fuelCapacity = data.fuelCapacity,
            maxThrustVac = data.maxThrustVac,
            ispVacuum = data.ispVacuum,
            prefabPath = prefabAssetPath,
            nodes = data.nodes ?? new List<AttachmentNode>(),
        };
    }

    static string ResolveFullPath(string assetPath)
    {
        string normalized = assetPath.Replace('/', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalized))
        {
            return normalized;
        }

        string projectRoot = Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(projectRoot, normalized));
    }

    static List<AttachmentNode> ParseNodeArray(string json, ref int index)
    {
        List<AttachmentNode> nodes = new List<AttachmentNode>();
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

            nodes.Add(ParseNode(json, ref index));
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ',')
            {
                index++;
            }
        }

        return nodes;
    }

    static AttachmentNode ParseNode(string json, ref int index)
    {
        AttachmentNode node = new AttachmentNode(SimVector3.Zero(), SimVector3.UnitY());
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
                case "localPosition":
                    node.localPosition = ReadVector3(json, ref index);
                    break;
                case "direction":
                    node.direction = ReadVector3(json, ref index);
                    break;
                case "role":
                    node.role = ReadString(json, ref index);
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

        return node;
    }

    static void AppendNodes(
        StringBuilder builder,
        List<AttachmentNode> nodes,
        bool prettyPrint,
        int indent
    )
    {
        builder.Append('"');
        builder.Append("nodes");
        builder.Append("\":");
        WriteArrayStart(builder, prettyPrint, indent);

        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                    AppendNewLine(builder, prettyPrint, indent + 1);
                }

                AppendNode(builder, nodes[i], prettyPrint, indent + 1);
            }
        }

        WriteArrayEnd(builder, prettyPrint, indent);
    }

    static void AppendNode(
        StringBuilder builder,
        AttachmentNode node,
        bool prettyPrint,
        int indent
    )
    {
        WriteObjectStart(builder, prettyPrint, indent);
        AppendVector3(builder, "localPosition", node.localPosition, prettyPrint, indent + 1);
        builder.Append(',');
        AppendNewLine(builder, prettyPrint, indent + 1);
        AppendVector3(builder, "direction", node.direction, prettyPrint, indent + 1);

        if (!string.IsNullOrWhiteSpace(node.role))
        {
            builder.Append(',');
            AppendNewLine(builder, prettyPrint, indent + 1);
            AppendString(builder, "role", node.role, prettyPrint, indent + 1);
        }

        WriteObjectEnd(builder, prettyPrint, indent);
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

    static void AppendFloat(
        StringBuilder builder,
        string key,
        float value,
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

    static SimVector3 ReadVector3(string json, ref int index)
    {
        SkipWhitespace(json, ref index);
        Expect(json, ref index, '[');
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

        throw new FormatException("Unterminated JSON string in part definition.");
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
            throw new FormatException("Invalid JSON number in part definition: " + token);
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
                "Expected '" + expected + "' in part definition JSON."
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
