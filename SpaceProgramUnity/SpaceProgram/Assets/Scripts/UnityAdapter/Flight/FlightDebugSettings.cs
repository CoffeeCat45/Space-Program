using System;
using UnityEngine;

public enum FlightDebugOverlay
{
    Fps = 0,
    Camera = 1,
    Configuration = 2,
    Scenarios = 3,
    Network = 4,
    Gfx = 5,
    Com = 6,
    LodChunks = 7,
    DeveloperSettings = 8
}

/// <summary>
/// Which flight debug chrome is drawn. Persisted in <c>settings.json</c> as
/// <c>debug.hiddenOverlays</c>. Default: all on.
/// </summary>
public static class FlightDebugSettings
{
    public readonly struct Item
    {
        public readonly FlightDebugOverlay Overlay;
        public readonly string SectionKey;
        public readonly string NameKey;

        public Item(FlightDebugOverlay overlay, string sectionKey, string nameKey)
        {
            Overlay = overlay;
            SectionKey = sectionKey;
            NameKey = nameKey;
        }
    }

    static readonly FlightDebugOverlay[] AllOverlays =
        (FlightDebugOverlay[])Enum.GetValues(typeof(FlightDebugOverlay));

    static readonly bool[] Visible = new bool[AllOverlays.Length];
    static bool loaded;

    public static readonly Item[] Items =
    {
        new Item(FlightDebugOverlay.Fps, "Debug overlays", "FPS panel"),
        new Item(FlightDebugOverlay.Camera, "Debug overlays", "Camera panel"),
        new Item(FlightDebugOverlay.LodChunks, "Debug overlays", "LOD chunks info"),
        new Item(FlightDebugOverlay.Configuration, "Debug buttons", "Configuration button"),
        new Item(FlightDebugOverlay.Scenarios, "Debug buttons", "Scenarios button"),
        new Item(FlightDebugOverlay.Network, "Debug buttons", "Network button"),
        new Item(FlightDebugOverlay.Gfx, "Debug buttons", "GFX button"),
        new Item(FlightDebugOverlay.Com, "Debug buttons", "CoM button"),
        new Item(FlightDebugOverlay.DeveloperSettings, "Debug buttons", "Developer settings button")
    };

    public static void Load()
    {
        GameSettingsStore.EnsureLoaded();
        bool[] stored = GameSettingsStore.ReadDebugOverlayVisibility();

        for (int i = 0; i < AllOverlays.Length && i < stored.Length; i++)
        {
            Visible[i] = stored[i];
        }

        loaded = true;
    }

    public static void Reload()
    {
        loaded = false;
        Load();
    }

    public static bool IsLoaded => loaded;

    public static void FlushIfLoaded()
    {
        if (!loaded)
        {
            return;
        }

        Persist();
    }

    public static void Unload()
    {
        loaded = false;
    }

    public static bool IsVisible(FlightDebugOverlay overlay)
    {
        if (!loaded)
        {
            Load();
        }

        int index = (int)overlay;
        if (index < 0 || index >= Visible.Length)
        {
            return true;
        }

        return Visible[index];
    }

    public static void SetVisible(FlightDebugOverlay overlay, bool visible)
    {
        if (!loaded)
        {
            Load();
        }

        int index = (int)overlay;
        if (index < 0 || index >= Visible.Length)
        {
            return;
        }

        if (Visible[index] == visible)
        {
            return;
        }

        Visible[index] = visible;
        Persist();
    }

    public static void SetAllVisible(bool visible)
    {
        if (!loaded)
        {
            Load();
        }

        for (int i = 0; i < Visible.Length; i++)
        {
            Visible[i] = visible;
        }

        Persist();
    }

    static void Persist()
    {
        GameSettingsStore.WriteDebugOverlayVisibility(Visible);
        GameSettingsStore.SaveToDisk();
    }
}

/// <summary>
/// Packed screen slots for the right-side debug buttons so hiding one does not
/// leave a hole.
/// </summary>
public static class FlightDebugChromeLayout
{
    const float TopRightWidth = 260f;
    const float TopRightMargin = 10f;
    const float TopRightGap = 8f;
    const float ConfigurationHeight = 28f;
    const float ScenariosHeight = 30f;
    const float NetworkHeight = 28f;
    const float MidRightWidth = 112f;
    const float MidRightHeight = 34f;
    const float MidRightMargin = 16f;
    const float MidRightGap = 8f;

    static readonly FlightDebugOverlay[] TopRightOrder =
    {
        FlightDebugOverlay.Configuration,
        FlightDebugOverlay.Scenarios,
        FlightDebugOverlay.Network
    };

    static readonly float[] TopRightHeights =
    {
        ConfigurationHeight,
        ScenariosHeight,
        NetworkHeight
    };

    static readonly FlightDebugOverlay[] MidRightOrder =
    {
        FlightDebugOverlay.Gfx,
        FlightDebugOverlay.Com
    };

    public static bool TryGetTopRightButtonRect(FlightDebugOverlay overlay, out Rect rect)
    {
        rect = default;
        float x = Screen.width - TopRightWidth - TopRightMargin;
        float y = TopRightMargin;

        for (int i = 0; i < TopRightOrder.Length; i++)
        {
            FlightDebugOverlay current = TopRightOrder[i];
            if (!FlightDebugSettings.IsVisible(current))
            {
                continue;
            }

            if (current == overlay)
            {
                rect = new Rect(x, y, TopRightWidth, TopRightHeights[i]);
                return true;
            }

            y += TopRightHeights[i] + TopRightGap;
        }

        return false;
    }

    public static float GetTopRightStackBottomY()
    {
        float y = TopRightMargin;
        bool any = false;

        for (int i = 0; i < TopRightOrder.Length; i++)
        {
            if (!FlightDebugSettings.IsVisible(TopRightOrder[i]))
            {
                continue;
            }

            y += TopRightHeights[i] + TopRightGap;
            any = true;
        }

        return any ? y : TopRightMargin;
    }

    public static bool TryGetMidRightButtonRect(FlightDebugOverlay overlay, out Rect rect)
    {
        rect = default;
        int stackIndex = 0;

        for (int i = 0; i < MidRightOrder.Length; i++)
        {
            FlightDebugOverlay current = MidRightOrder[i];
            if (!FlightDebugSettings.IsVisible(current))
            {
                continue;
            }

            if (current == overlay)
            {
                const float buttonWidth = MidRightWidth;
                const float buttonHeight = MidRightHeight;
                float x = Screen.width - buttonWidth - MidRightMargin;
                float bottomY = Screen.height * 0.58f - buttonHeight * 0.5f;
                float y = bottomY - (1 - stackIndex) * (buttonHeight + MidRightGap);
                rect = new Rect(x, y, buttonWidth, buttonHeight);
                return true;
            }

            stackIndex++;
        }

        return false;
    }
}
