using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Rebindable flight keys persisted in <c>settings.json</c>. Default cycle key is
/// backtick, same as KSP navball Surface/Orbit/Target.
/// </summary>
public static class FlightKeyBindings
{
    public const Key DefaultSpeedReferenceCycleKey = Key.Backquote;

    static bool loaded;
    static bool capturingSpeedReferenceCycle;

    public static Key SpeedReferenceCycleKey { get; private set; } =
        DefaultSpeedReferenceCycleKey;

    public static bool IsCapturingSpeedReferenceCycle => capturingSpeedReferenceCycle;

    public static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        Reload();
    }

    public static void Reload()
    {
        GameSettingsStore.EnsureLoaded();
        SpeedReferenceCycleKey = ParseKey(
            GameSettingsStore.Data.play.speedReferenceCycleKey,
            DefaultSpeedReferenceCycleKey
        );
        loaded = true;
    }

    public static void StartCaptureSpeedReferenceCycle()
    {
        EnsureLoaded();
        capturingSpeedReferenceCycle = true;
    }

    public static void CancelCapture()
    {
        capturingSpeedReferenceCycle = false;
    }

    public static bool TryHandleSessionEscape()
    {
        if (!capturingSpeedReferenceCycle)
        {
            return false;
        }

        Keyboard keyboard = Keyboard.current;
        if (
            keyboard != null &&
            keyboard.escapeKey != null &&
            keyboard.escapeKey.wasPressedThisFrame
        )
        {
            CancelCapture();
        }

        return true;
    }

    public static bool TryCompleteCapture()
    {
        if (!capturingSpeedReferenceCycle)
        {
            return false;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        if (
            keyboard.escapeKey != null &&
            keyboard.escapeKey.wasPressedThisFrame
        )
        {
            CancelCapture();
            return true;
        }

        foreach (Key key in Enum.GetValues(typeof(Key)))
        {
            if (
                key == Key.None ||
                key == Key.IMESelected ||
                key == Key.Escape
            )
            {
                continue;
            }

            KeyControl control = keyboard[key];
            if (control != null && control.wasPressedThisFrame)
            {
                SetSpeedReferenceCycleKey(key);
                capturingSpeedReferenceCycle = false;
                return true;
            }
        }

        return false;
    }

    public static bool WasSpeedReferenceCyclePressed()
    {
        EnsureLoaded();

        if (capturingSpeedReferenceCycle)
        {
            return false;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        KeyControl control = keyboard[SpeedReferenceCycleKey];
        return control != null && control.wasPressedThisFrame;
    }

    public static string FormatSpeedReferenceCycleKey()
    {
        EnsureLoaded();
        return FormatKey(SpeedReferenceCycleKey);
    }

    public static Key ParseKey(string stored, Key fallback)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return fallback;
        }

        return Enum.TryParse(stored, ignoreCase: true, out Key parsed) &&
            parsed != Key.None &&
            parsed != Key.IMESelected ?
            parsed :
            fallback;
    }

    public static string FormatKey(Key key)
    {
        switch (key)
        {
            case Key.Backquote:
                return "`";
            case Key.Quote:
                return "'";
            case Key.Semicolon:
                return ";";
            case Key.Comma:
                return ",";
            case Key.Period:
                return ".";
            case Key.Slash:
                return "/";
            case Key.Backslash:
                return "\\";
            case Key.LeftBracket:
                return "[";
            case Key.RightBracket:
                return "]";
            case Key.Minus:
                return "-";
            case Key.Equals:
                return "=";
            case Key.Space:
                return "Space";
            default:
                return key.ToString();
        }
    }

    static void SetSpeedReferenceCycleKey(Key key)
    {
        SpeedReferenceCycleKey = key;
        GameSettingsStore.EnsureLoaded();
        GameSettingsStore.Data.play.speedReferenceCycleKey = key.ToString();
        GameSettingsStore.SaveToDisk();
        loaded = true;
    }
}
