using System.Globalization;
using UnityEngine;

public enum FlightCameraFollowKind
{
    Horizon,
    Inertial,
    ShipLock,
    PlanetPos,
    Map,
    Mixed
}

public enum FlightCameraPlanetScreen
{
    Down,
    Left,
    Right,
    Up,
    Forward,
    Behind,
    Unknown
}

public readonly struct FlightCameraDiagSnapshot
{
    public readonly FlightCameraFollowKind Kind;
    public readonly FlightCameraPlanetScreen PlanetScreen;
    public readonly string CompactLine;
    public readonly string FullReport;
    public readonly float DistCameraToShip;
    public readonly float DistCameraToPlanet;
    public readonly float DistPivotToShip;
    public readonly float UpAngleToUnityY;
    public readonly float UpAngleToShipUp;
    public readonly float UpAngleToInertial;

    public FlightCameraDiagSnapshot(
        FlightCameraFollowKind kind,
        FlightCameraPlanetScreen planetScreen,
        string compactLine,
        string fullReport,
        float distCameraToShip,
        float distCameraToPlanet,
        float distPivotToShip,
        float upAngleToUnityY,
        float upAngleToShipUp,
        float upAngleToInertial
    )
    {
        Kind = kind;
        PlanetScreen = planetScreen;
        CompactLine = compactLine;
        FullReport = fullReport;
        DistCameraToShip = distCameraToShip;
        DistCameraToPlanet = distCameraToPlanet;
        DistPivotToShip = distPivotToShip;
        UpAngleToUnityY = upAngleToUnityY;
        UpAngleToShipUp = upAngleToShipUp;
        UpAngleToInertial = upAngleToInertial;
    }
}

/// <summary>
/// Classifies Game-view chase: globe follow vs dual-frame zenith lock.
/// F7 overlay / FPS second line — paste into chat.
/// </summary>
public static class FlightCameraDiag
{
    public const float HorizonUpMaxDegrees = 25.0f;
    public const float InertialUpMaxDegrees = 20.0f;
    public const float ShipLockUpMaxDegrees = 15.0f;
    public const float PlanetPosNearerSlackMeters = 50.0f;

    public static FlightCameraDiagSnapshot Build(
        Vector3 cameraPosition,
        Quaternion cameraRotation,
        Vector3 pivotPosition,
        Vector3 shipPosition,
        Vector3 planetPosition,
        Vector3 shipUp,
        Vector3 inertialUp,
        string soiName,
        string focusName,
        bool padLocked,
        bool mapMode,
        bool chaseMode,
        float yawDegrees,
        float pitchDegrees,
        float orbitFrameAngleFromIdentity,
        float orbitDistance
    )
    {
        float distCamShip = Vector3.Distance(cameraPosition, shipPosition);
        float distCamPlanet = Vector3.Distance(cameraPosition, planetPosition);
        float distPivotShip = Vector3.Distance(pivotPosition, shipPosition);
        Vector3 camUp = cameraRotation * Vector3.up;
        float upY = Vector3.Angle(camUp, Vector3.up);
        float upShip = AngleOr180(camUp, shipUp);
        float upInertial = AngleOr180(camUp, inertialUp);
        FlightCameraPlanetScreen planetScreen = ClassifyPlanetScreen(
            cameraPosition,
            cameraRotation,
            planetPosition
        );
        FlightCameraFollowKind kind = ClassifyKind(
            mapMode,
            chaseMode,
            distCamShip,
            distCamPlanet,
            distPivotShip,
            upY,
            upShip,
            upInertial
        );

        string kindToken = KindToken(kind);
        string compact = string.Format(
            CultureInfo.InvariantCulture,
            "cam {0}  pivot={1} {2}  planet={3}  upY={4:0}°  inertΔ={5:0}°  SoI={6}",
            kindToken,
            string.IsNullOrEmpty(focusName) ? "?" : focusName,
            FormatDistance(distCamShip),
            PlanetScreenToken(planetScreen),
            upY,
            upInertial,
            string.IsNullOrEmpty(soiName) ? "?" : soiName
        );

        string full = string.Format(
            CultureInfo.InvariantCulture,
            "CAM DIAG  F7=показать+лог  Shift+F7=скрыть\n" +
            "kind={0}\n" +
            "{1}\n" +
            "planetInView={2}  (LEFT/RIGHT = «стена» сбоку; DOWN = зенит-lock)\n" +
            "focus={3}  SoI={4}  pad={5}  map={6}  chase={7}\n" +
            "dist cam→ship={8}  cam→planet={9}  pivot→ship={10}  orbit={11}\n" +
            "camUp∠+Y={12:0.0}°  camUp∠ship={13:0.0}°  camUp∠inertial={14:0.0}°\n" +
            "yaw={15:0.0}  pitch={16:0.0}  frame∠id={17:0.0}°\n" +
            "cam={18}  pivot={19}  planet={20}",
            kindToken,
            KindMeaning(kind),
            PlanetScreenToken(planetScreen),
            string.IsNullOrEmpty(focusName) ? "?" : focusName,
            string.IsNullOrEmpty(soiName) ? "?" : soiName,
            padLocked ? 1 : 0,
            mapMode ? 1 : 0,
            chaseMode ? 1 : 0,
            FormatDistance(distCamShip),
            FormatDistance(distCamPlanet),
            FormatDistance(distPivotShip),
            FormatDistance(orbitDistance),
            upY,
            upShip,
            upInertial,
            yawDegrees,
            pitchDegrees,
            orbitFrameAngleFromIdentity,
            FormatVector(cameraPosition),
            FormatVector(pivotPosition),
            FormatVector(planetPosition)
        );

        return new FlightCameraDiagSnapshot(
            kind,
            planetScreen,
            compact,
            full,
            distCamShip,
            distCamPlanet,
            distPivotShip,
            upY,
            upShip,
            upInertial
        );
    }

    public static FlightCameraFollowKind ClassifyKind(
        bool mapMode,
        bool chaseMode,
        float distCameraToShip,
        float distCameraToPlanet,
        float distPivotToShip,
        float upAngleToUnityY,
        float upAngleToShipUp,
        float upAngleToInertial
    )
    {
        if (mapMode)
        {
            return FlightCameraFollowKind.Map;
        }

        if (
            distCameraToPlanet + PlanetPosNearerSlackMeters < distCameraToShip ||
            (
                distPivotToShip > 200.0f &&
                distCameraToPlanet < distCameraToShip * 0.25f
            )
        )
        {
            return FlightCameraFollowKind.PlanetPos;
        }

        if (
            !chaseMode &&
            upAngleToShipUp <= ShipLockUpMaxDegrees &&
            upAngleToUnityY > HorizonUpMaxDegrees
        )
        {
            return FlightCameraFollowKind.ShipLock;
        }

        if (
            upAngleToInertial <= InertialUpMaxDegrees &&
            upAngleToUnityY > HorizonUpMaxDegrees
        )
        {
            return FlightCameraFollowKind.Inertial;
        }

        if (upAngleToUnityY <= HorizonUpMaxDegrees)
        {
            return FlightCameraFollowKind.Horizon;
        }

        return FlightCameraFollowKind.Mixed;
    }

    public static FlightCameraPlanetScreen ClassifyPlanetScreen(
        Vector3 cameraPosition,
        Quaternion cameraRotation,
        Vector3 planetPosition
    )
    {
        Vector3 toPlanet = planetPosition - cameraPosition;
        if (toPlanet.sqrMagnitude <= 1e-8f)
        {
            return FlightCameraPlanetScreen.Unknown;
        }

        Vector3 local = Quaternion.Inverse(cameraRotation) * toPlanet.normalized;
        float absX = Mathf.Abs(local.x);
        float absY = Mathf.Abs(local.y);
        float absZ = Mathf.Abs(local.z);

        if (absY >= absX && absY >= absZ)
        {
            return local.y < 0.0f ?
                FlightCameraPlanetScreen.Down :
                FlightCameraPlanetScreen.Up;
        }

        if (absX >= absZ)
        {
            return local.x > 0.0f ?
                FlightCameraPlanetScreen.Right :
                FlightCameraPlanetScreen.Left;
        }

        return local.z > 0.0f ?
            FlightCameraPlanetScreen.Forward :
            FlightCameraPlanetScreen.Behind;
    }

    static float AngleOr180(Vector3 a, Vector3 b)
    {
        if (a.sqrMagnitude <= 1e-10f || b.sqrMagnitude <= 1e-10f)
        {
            return 180.0f;
        }

        return Vector3.Angle(a, b);
    }

    static string KindToken(FlightCameraFollowKind kind)
    {
        switch (kind)
        {
            case FlightCameraFollowKind.Horizon:
                return "HORIZON";
            case FlightCameraFollowKind.Inertial:
                return "INERTIAL";
            case FlightCameraFollowKind.ShipLock:
                return "SHIP";
            case FlightCameraFollowKind.PlanetPos:
                return "PLANET-POS";
            case FlightCameraFollowKind.Map:
                return "MAP";
            default:
                return "MIXED";
        }
    }

    static string PlanetScreenToken(FlightCameraPlanetScreen screen)
    {
        switch (screen)
        {
            case FlightCameraPlanetScreen.Down:
                return "DOWN";
            case FlightCameraPlanetScreen.Left:
                return "LEFT";
            case FlightCameraPlanetScreen.Right:
                return "RIGHT";
            case FlightCameraPlanetScreen.Up:
                return "UP";
            case FlightCameraPlanetScreen.Forward:
                return "FWD";
            case FlightCameraPlanetScreen.Behind:
                return "BACK";
            default:
                return "?";
        }
    }

    static string KindMeaning(FlightCameraFollowKind kind)
    {
        switch (kind)
        {
            case FlightCameraFollowKind.Horizon:
                return "камера у корабля, +Y вверх. Планета сидит ВНИЗУ Game — dual-frame, не chase глобуса.";
            case FlightCameraFollowKind.PlanetPos:
                return "камера ближе к центру планеты, чем к кораблю — реальное слежение за SoI-телом.";
            case FlightCameraFollowKind.Inertial:
                return "up как у skybox. Планета должна уезжать по кадру; корпус может казаться накренённым.";
            case FlightCameraFollowKind.ShipLock:
                return "up с осями корпуса. Q/E крутили бы всю картинку (KSP LOCKED).";
            case FlightCameraFollowKind.Map:
                return "режим карты M.";
            default:
                return "смесь: up не +Y, не skybox и не корпус. Скрин + эта панель.";
        }
    }

    public static string FormatDistance(float meters)
    {
        float abs = Mathf.Abs(meters);
        if (abs < 1000.0f)
        {
            return meters.ToString("0.0", CultureInfo.InvariantCulture) + "m";
        }

        return meters.ToString("0.00e0", CultureInfo.InvariantCulture) + "m";
    }

    static string FormatVector(Vector3 value)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "({0:0.0},{1:0.0},{2:0.0})",
            value.x,
            value.y,
            value.z
        );
    }
}
