using UnityEngine;

/// <summary>
/// Switches the main camera between flight (LocalPhysics) and map (ScaledSpace) layers.
/// </summary>
static class FlightCameraLayerMask
{
    public static void ApplyFlightMode(Camera camera)
    {
        Apply(camera, mapModeActive: false);
    }

    public static void ApplyMapMode(Camera camera)
    {
        Apply(camera, mapModeActive: true);
    }

    public static void Apply(Camera camera, bool mapModeActive)
    {
        if (camera == null)
        {
            return;
        }

        if (!FlightRenderLayers.HasDedicatedLayers())
        {
            return;
        }

        int localPhysicsMask = FlightRenderLayers.LocalPhysicsMask;
        int scaledSpaceMask = FlightRenderLayers.ScaledSpaceMask;

        if (mapModeActive)
        {
            camera.cullingMask &= ~localPhysicsMask;
            camera.cullingMask |= scaledSpaceMask;
        }
        else
        {
            // Flight mode: render everything except the map-only ScaledSpace globe.
            camera.cullingMask = ~scaledSpaceMask;
        }
    }
}
