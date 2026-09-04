using System;
using System.Collections.Generic;

/// <summary>
/// Attachment point on a part template (magnetic hatch / stack node).
/// Positions and directions are in part-local space (meters, SimVector3).
/// </summary>
public static class PartAttachmentRoles
{
    public const string StackTop = "stack_top";
    public const string StackBottom = "stack_bottom";
    public const string Attach = "attach";
    public const string Surface = "surface";
}

public class AttachmentNode
{
    public SimVector3 localPosition;
    public SimVector3 direction;
    /// <summary>Optional KSP role: stack_top, stack_bottom, attach, surface, etc.</summary>
    public string role;

    public AttachmentNode(SimVector3 localPosition, SimVector3 direction, string role = null)
    {
        this.localPosition = localPosition;
        this.direction = direction;
        this.role = role;
    }
}

/// <summary>
/// Static definition of a buildable part for the VAB editor.
/// Unity-free — prefabPath is resolved in UnityAdapter.
/// </summary>
public class PartTemplate
{
    public string id;
    public string displayName;
    public string category;
    public float dryMass;
    public float fuelCapacity;
    /// <summary>Vacuum max thrust in kN (engines only).</summary>
    public float maxThrustVac;
    /// <summary>Vacuum specific impulse in seconds (engines only).</summary>
    public float ispVacuum;
    public string prefabPath;
    /// <summary>Asset path to generated PNG icon, e.g. Assets/Models/Parts/Icons/@thumbs/{id}_icon.png</summary>
    public string iconPath;
    public List<AttachmentNode> nodes = new List<AttachmentNode>();
}

/// <summary>
/// Runtime catalog of VAB parts. Populated by <c>PartCatalog.Refresh()</c> in UnityAdapter.
/// Pilot stack profiles: <see cref="PartStackProfileLibrary"/>.
/// </summary>
public static class PartDatabase
{
    public const string CategoryCommand = "Command";
    public const string CategoryFuelTank = "FuelTank";
    public const string CategoryEngine = "Engine";
    public const string CategoryMisc = "Misc";

    static readonly string[] PreferredCategoryOrder =
    {
        CategoryCommand,
        CategoryFuelTank,
        CategoryEngine,
        CategoryMisc,
    };

    static readonly List<PartTemplate> templates = new List<PartTemplate>();

    public static IReadOnlyList<PartTemplate> Templates => templates;

    public static void ReplaceTemplates(List<PartTemplate> discoveredTemplates)
    {
        templates.Clear();

        if (discoveredTemplates == null)
        {
            return;
        }

        templates.AddRange(discoveredTemplates);
    }

    public static IReadOnlyList<string> GetCategories()
    {
        List<string> ordered = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < PreferredCategoryOrder.Length; i++)
        {
            string category = PreferredCategoryOrder[i];

            if (HasCategory(category) && seen.Add(category))
            {
                ordered.Add(category);
            }
        }

        List<string> extras = new List<string>();

        for (int i = 0; i < templates.Count; i++)
        {
            string category = templates[i].category;

            if (string.IsNullOrWhiteSpace(category) || seen.Contains(category))
            {
                continue;
            }

            seen.Add(category);
            extras.Add(category);
        }

        extras.Sort(StringComparer.OrdinalIgnoreCase);
        ordered.AddRange(extras);
        return ordered;
    }

    public static PartTemplate GetById(string templateId)
    {
        if (string.IsNullOrEmpty(templateId))
        {
            return null;
        }

        for (int i = 0; i < templates.Count; i++)
        {
            if (templates[i].id.Equals(templateId, StringComparison.Ordinal))
            {
                return templates[i];
            }
        }

        return null;
    }

    public static List<PartTemplate> GetByCategory(string category)
    {
        List<PartTemplate> result = new List<PartTemplate>();

        if (string.IsNullOrEmpty(category))
        {
            return result;
        }

        for (int i = 0; i < templates.Count; i++)
        {
            if (templates[i].category.Equals(category, StringComparison.Ordinal))
            {
                result.Add(templates[i]);
            }
        }

        result.Sort((left, right) =>
            string.Compare(left.displayName, right.displayName, StringComparison.OrdinalIgnoreCase)
        );

        return result;
    }

    static bool HasCategory(string category)
    {
        for (int i = 0; i < templates.Count; i++)
        {
            if (templates[i].category.Equals(category, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
