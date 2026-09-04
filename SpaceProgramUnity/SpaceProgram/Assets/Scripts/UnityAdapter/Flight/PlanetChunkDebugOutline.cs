using UnityEngine;

/// <summary>
/// URP-safe runtime wireframe outline for one planet LOD chunk (LineRenderer).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class PlanetChunkDebugOutline : MonoBehaviour
{
    const float ScreenSpaceLineWidth = 1.5f;
    const int MaxLodLevelReference = 14;

    LineRenderer lineRenderer;
    int lodLevel;
    bool isVisible;

    public void Configure(int level, float configuredLineWidth)
    {
        lodLevel = level;
        EnsureLineRenderer(configuredLineWidth);
        ApplyLodColor();
        SetVisible(isVisible);
    }

    public void SetCorners(Vector3[] cornersLocal)
    {
        if (cornersLocal == null || cornersLocal.Length < 4)
        {
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = cornersLocal[i];
            if (float.IsNaN(corner.x) || float.IsNaN(corner.y) || float.IsNaN(corner.z) ||
                float.IsInfinity(corner.x) || float.IsInfinity(corner.y) || float.IsInfinity(corner.z))
            {
                SetVisible(false);
                return;
            }
        }

        EnsureLineRenderer(ScreenSpaceLineWidth);
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = 5;
        lineRenderer.SetPosition(0, cornersLocal[0]);
        lineRenderer.SetPosition(1, cornersLocal[1]);
        lineRenderer.SetPosition(2, cornersLocal[2]);
        lineRenderer.SetPosition(3, cornersLocal[3]);
        lineRenderer.SetPosition(4, cornersLocal[0]);
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        EnsureLineRenderer(ScreenSpaceLineWidth);

        if (lineRenderer != null)
        {
            lineRenderer.enabled = visible;
        }
    }

    void LateUpdate()
    {
        if (!isVisible || lineRenderer == null)
        {
            return;
        }

        lineRenderer.widthMultiplier = ScreenSpaceLineWidth;
    }

    void EnsureLineRenderer(float configuredLineWidth)
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();

            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            lineRenderer.useWorldSpace = false;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            lineRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            lineRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            lineRenderer.loop = true;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.generateLightingData = false;
            lineRenderer.material = RuntimeMaterialFactory.CreateLineMaterial();
        }

        lineRenderer.widthMultiplier = Mathf.Max(ScreenSpaceLineWidth, configuredLineWidth);
        ApplyLodColor();
    }

    void ApplyLodColor()
    {
        if (lineRenderer == null)
        {
            return;
        }

        float lodT = Mathf.Clamp01((float)lodLevel / MaxLodLevelReference);
        Color color = Color.Lerp(Color.green, Color.red, lodT);
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        if (lineRenderer.material != null)
        {
            lineRenderer.material.color = color;
        }
    }

    void OnDestroy()
    {
        if (lineRenderer != null && lineRenderer.material != null)
        {
            Destroy(lineRenderer.material);
        }
    }
}
