using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Baked VAB part list plus pad prefab references for the standalone player.
/// Editor Play still scans <c>Assets/Prefabs/Parts</c>; the player loads this asset from Resources.
/// </summary>
[CreateAssetMenu(menuName = "Space Program/Runtime Part Catalog")]
public sealed class RuntimePartCatalog : ScriptableObject
{
    public const string ResourcesAssetName = "RuntimePartCatalog";
    public const string AssetPath = "Assets/Resources/RuntimePartCatalog.asset";

    [Serializable]
    public sealed class NodeRecord
    {
        public Vector3 localPosition;
        public Vector3 direction;
        public string role;
    }

    [Serializable]
    public sealed class PartRecord
    {
        public string id;
        public string displayName;
        public string category;
        public float dryMass;
        public float fuelCapacity;
        public float maxThrustVac;
        public float ispVacuum;
        public string prefabPath;
        public string iconPath;
        public GameObject prefab;
        public Texture2D icon;
        public NodeRecord[] nodes;
        public Vector3 thrustOffsetPartLocal;
        public Vector3 thrustForceDirectionPartLocal = Vector3.up;
        public bool hasThrustFrame;
    }

    public GameObject coveLaunchSite;
    public PartRecord[] parts;

    static RuntimePartCatalog cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        cached = null;
    }

    public static RuntimePartCatalog Load()
    {
        if (cached != null)
        {
            return cached;
        }

        cached = Resources.Load<RuntimePartCatalog>(ResourcesAssetName);
        return cached;
    }

    public GameObject GetPrefab(string partTemplateId)
    {
        PartRecord record = FindRecord(partTemplateId);
        return record != null ? record.prefab : null;
    }

    public GameObject GetPrefabByAssetPath(string prefabPath)
    {
        if (string.IsNullOrWhiteSpace(prefabPath) || parts == null)
        {
            return null;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            PartRecord record = parts[i];

            if (
                record != null &&
                !string.IsNullOrWhiteSpace(record.prefabPath) &&
                record.prefabPath.Equals(prefabPath, StringComparison.OrdinalIgnoreCase)
            )
            {
                return record.prefab;
            }
        }

        return null;
    }

    public Texture2D GetIcon(string partTemplateId)
    {
        PartRecord record = FindRecord(partTemplateId);
        return record != null ? record.icon : null;
    }

    public Texture2D GetIconByAssetPath(string iconAssetPath)
    {
        if (string.IsNullOrWhiteSpace(iconAssetPath) || parts == null)
        {
            return null;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            PartRecord record = parts[i];

            if (
                record != null &&
                !string.IsNullOrWhiteSpace(record.iconPath) &&
                record.iconPath.Equals(iconAssetPath, StringComparison.OrdinalIgnoreCase)
            )
            {
                return record.icon;
            }
        }

        return null;
    }

    public bool TryGetThrustFrame(
        string partTemplateId,
        out SimVector3 thrustOffsetPartLocal,
        out SimVector3 thrustForceDirectionPartLocal
    )
    {
        thrustOffsetPartLocal = SimVector3.Zero();
        thrustForceDirectionPartLocal = SimVector3.UnitY();
        PartRecord record = FindRecord(partTemplateId);

        if (record == null || !record.hasThrustFrame)
        {
            return false;
        }

        thrustOffsetPartLocal = new SimVector3(
            record.thrustOffsetPartLocal.x,
            record.thrustOffsetPartLocal.y,
            record.thrustOffsetPartLocal.z
        );
        thrustForceDirectionPartLocal = new SimVector3(
            record.thrustForceDirectionPartLocal.x,
            record.thrustForceDirectionPartLocal.y,
            record.thrustForceDirectionPartLocal.z
        );
        return true;
    }

    public List<PartTemplate> ToTemplates()
    {
        List<PartTemplate> templates = new List<PartTemplate>();

        if (parts == null)
        {
            return templates;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            PartRecord record = parts[i];

            if (record == null || string.IsNullOrWhiteSpace(record.id))
            {
                continue;
            }

            PartTemplate template = new PartTemplate
            {
                id = record.id,
                displayName = record.displayName,
                category = record.category,
                dryMass = record.dryMass,
                fuelCapacity = record.fuelCapacity,
                maxThrustVac = record.maxThrustVac,
                ispVacuum = record.ispVacuum,
                prefabPath = record.prefabPath,
                iconPath = record.iconPath,
                nodes = new List<AttachmentNode>(),
            };

            if (record.nodes != null)
            {
                for (int nodeIndex = 0; nodeIndex < record.nodes.Length; nodeIndex++)
                {
                    NodeRecord node = record.nodes[nodeIndex];

                    if (node == null)
                    {
                        continue;
                    }

                    template.nodes.Add(
                        new AttachmentNode(
                            new SimVector3(node.localPosition.x, node.localPosition.y, node.localPosition.z),
                            new SimVector3(node.direction.x, node.direction.y, node.direction.z),
                            node.role
                        )
                    );
                }
            }

            templates.Add(template);
        }

        return templates;
    }

    PartRecord FindRecord(string partTemplateId)
    {
        if (string.IsNullOrWhiteSpace(partTemplateId) || parts == null)
        {
            return null;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            PartRecord record = parts[i];

            if (
                record != null &&
                !string.IsNullOrWhiteSpace(record.id) &&
                record.id.Equals(partTemplateId, StringComparison.OrdinalIgnoreCase)
            )
            {
                return record;
            }
        }

        return null;
    }
}
