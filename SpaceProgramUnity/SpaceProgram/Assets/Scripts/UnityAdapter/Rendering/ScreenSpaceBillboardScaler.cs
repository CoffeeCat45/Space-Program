using UnityEngine;

/// <summary>
/// KSP-style constant screen-size billboards: world size from camera FOV and distance.
/// Map quads: lifted along planet radial, rotation faces the active camera (billboard).
/// </summary>
[DefaultExecutionOrder(250)]
[DisallowMultipleComponent]
public sealed class ScreenSpaceBillboardScaler : MonoBehaviour
{
    public enum BillboardMode
    {
        CameraFacing,
        SurfacePin,
    }

    [SerializeField]
    float screenHeightFraction = 0.028f;

    [SerializeField]
    bool driveTextMeshCharacterSize;

    [SerializeField]
    float textMeshSizeMultiplier = 1f;

    [SerializeField]
    BillboardMode billboardMode = BillboardMode.CameraFacing;

    public bool DriveTextMeshCharacterSize
    {
        get => driveTextMeshCharacterSize;
        set => driveTextMeshCharacterSize = value;
    }

    public BillboardMode Mode
    {
        get => billboardMode;
        set => billboardMode = value;
    }

    TextMesh textMesh;
    float lastAppliedWorldSize = float.NaN;
    const float WorldSizeChangeThreshold = 0.02f;

    public float ScreenHeightFraction
    {
        get => screenHeightFraction;
        set => screenHeightFraction = value;
    }

    public float TextMeshSizeMultiplier
    {
        get => textMeshSizeMultiplier;
        set => textMeshSizeMultiplier = Mathf.Max(value, 0.01f);
    }

    void Awake()
    {
        if (driveTextMeshCharacterSize)
        {
            textMesh = GetComponent<TextMesh>();
        }
    }

    void LateUpdate()
    {
        Camera facingCamera = ResolveFacingCamera();

        if (facingCamera == null)
        {
            return;
        }

        Vector3 anchorWorldPosition = ResolveAnchorWorldPosition();
        float distance = Vector3.Distance(
            anchorWorldPosition,
            facingCamera.transform.position
        );
        float verticalFovRadians = facingCamera.fieldOfView * Mathf.Deg2Rad;
        float screenWorldHeight = 2.0f * distance * Mathf.Tan(verticalFovRadians * 0.5f);
        float markerWorldSize = screenWorldHeight * screenHeightFraction;
        bool sizeChanged =
            float.IsNaN(lastAppliedWorldSize) ||
            Mathf.Abs(markerWorldSize - lastAppliedWorldSize) >
            lastAppliedWorldSize * WorldSizeChangeThreshold;

        if (billboardMode == BillboardMode.SurfacePin && !driveTextMeshCharacterSize)
        {
            Vector3 pinPosition = MapMarkerPlacement.ComputeSurfacePinPosition(
                anchorWorldPosition,
                markerWorldSize
            );
            transform.position = pinPosition;
            transform.rotation = facingCamera.transform.rotation;

            if (sizeChanged)
            {
                lastAppliedWorldSize = markerWorldSize;
                transform.localScale = new Vector3(markerWorldSize, markerWorldSize, 1.0f);
            }

            return;
        }

        transform.rotation = facingCamera.transform.rotation;

        if (driveTextMeshCharacterSize)
        {
            if (textMesh == null)
            {
                textMesh = GetComponent<TextMesh>();
            }

            if (textMesh != null && sizeChanged)
            {
                lastAppliedWorldSize = markerWorldSize;
                textMesh.characterSize =
                    markerWorldSize *
                    textMeshSizeMultiplier /
                    Mathf.Max(textMesh.fontSize, 1);
            }

            return;
        }

        if (sizeChanged)
        {
            lastAppliedWorldSize = markerWorldSize;
            transform.localScale = new Vector3(markerWorldSize, markerWorldSize, 1.0f);
        }
    }

    Vector3 ResolveAnchorWorldPosition()
    {
        if (transform.parent != null)
        {
            return transform.parent.position;
        }

        return transform.position;
    }

    static Camera ResolveFacingCamera()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            return mainCamera;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.SceneView sceneView = UnityEditor.SceneView.lastActiveSceneView;

            if (sceneView != null && sceneView.camera != null)
            {
                return sceneView.camera;
            }
        }
#endif

        return null;
    }
}
