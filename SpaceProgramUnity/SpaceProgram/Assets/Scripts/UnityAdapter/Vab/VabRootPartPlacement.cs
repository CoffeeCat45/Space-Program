using UnityEngine;

/// <summary>
/// Places the root part so its bottom stack node sits on the VAB work-center plane.
/// </summary>
static class VabRootPartPlacement
{
    public static Vector3 GetLocalPosition(PartTemplate template)
    {
        float bottomLocalY = GetBottomStackLocalY(template);
        return new Vector3(
            VabRootAnchor.LocalPosition.x,
            VabRootAnchor.HeightMeters - bottomLocalY,
            VabRootAnchor.LocalPosition.z
        );
    }

    public static SimVector3 GetSimLocalPosition(PartTemplate template)
    {
        Vector3 local = GetLocalPosition(template);
        return new SimVector3(local.x, local.y, local.z);
    }

    static float GetBottomStackLocalY(PartTemplate template)
    {
        if (template?.nodes == null || template.nodes.Count == 0)
        {
            return 0f;
        }

        for (int i = 0; i < template.nodes.Count; i++)
        {
            AttachmentNode node = template.nodes[i];

            if (node.role == PartAttachmentRoles.StackBottom)
            {
                return (float)node.localPosition.y;
            }
        }

        for (int i = 0; i < template.nodes.Count; i++)
        {
            AttachmentNode node = template.nodes[i];
            Vector3 direction = new Vector3(
                (float)node.direction.x,
                (float)node.direction.y,
                (float)node.direction.z
            );

            if (direction.y < -0.85f)
            {
                return (float)node.localPosition.y;
            }
        }

        return 0f;
    }
}
