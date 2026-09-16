using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class ShareBuildExporterTests
{
    [Test]
    public void JsonUtility_RoundTripsHudHiddenFieldsAndBackground()
    {
        var data = new GameSettingsData();
        data.hud.language = "Russian";
        data.hud.background = "Dim";
        data.hud.hiddenFields = new[] { "Keys", "Language", "MissionHint" };
        data.debug.hiddenOverlays = new[] { "Fps", "Gfx", "DeveloperSettings" };
        data.play.speedReferenceCycleKey = "N";

        string json = JsonUtility.ToJson(data, prettyPrint: true);
        var parsed = new GameSettingsData();
        JsonUtility.FromJsonOverwrite(json, parsed);

        Assert.AreEqual("Russian", parsed.hud.language);
        Assert.AreEqual("Dim", parsed.hud.background);
        CollectionAssert.AreEqual(
            new[] { "Keys", "Language", "MissionHint" },
            parsed.hud.hiddenFields
        );
        CollectionAssert.AreEqual(
            new[] { "Fps", "Gfx", "DeveloperSettings" },
            parsed.debug.hiddenOverlays
        );
        Assert.AreEqual("N", parsed.play.speedReferenceCycleKey);
    }

    [Test]
    public void TryCopyEditorSettingsJson_WritesAuthorSettingsNextToDestination()
    {
        FlightHudSettings.Unload();
        FlightDebugSettings.Unload();
        Assert.IsTrue(File.Exists(GameSettingsStore.FilePath));

        string destination = Path.Combine(
            Path.GetTempPath(),
            "ShareBuildSettingsTest-" + System.Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(destination);

        try
        {
            File.WriteAllText(Path.Combine(destination, GameSettingsStore.FileName), "{ }");
            Assert.IsTrue(ShareBuildExporter.TryCopyEditorSettingsJson(destination));

            string copiedPath = Path.Combine(destination, GameSettingsStore.FileName);
            Assert.IsTrue(File.Exists(copiedPath));
            Assert.AreEqual(
                File.ReadAllText(GameSettingsStore.FilePath),
                File.ReadAllText(copiedPath)
            );
        }
        finally
        {
            Directory.Delete(destination, recursive: true);
        }
    }

    [Test]
    public void TryCopyEditorSettingsJson_FlushesLoadedHudIntoCopiedFile()
    {
        WithRestoredEditorSettings(() =>
        {
            FlightHudSettings.Load();
            FlightHudSettings.SetAllVisible(true);
            FlightHudSettings.SetBackground(FlightHudBackground.Dark);
            FlightHudSettings.SetVisible(FlightHudField.Keys, false);
            FlightHudSettings.SetVisible(FlightHudField.SpeedAccel, false);
            FlightHudSettings.SetVisible(FlightHudField.MassFuel, true);

            string destination = CreateTempDirectory("ShareBuildHudFlush-");
            try
            {
                Assert.IsTrue(ShareBuildExporter.TryCopyEditorSettingsJson(destination));
                GameSettingsHud hud = ParseHud(
                    File.ReadAllText(Path.Combine(destination, GameSettingsStore.FileName))
                );
                Assert.AreEqual("Dark", hud.background);
                CollectionAssert.Contains(hud.hiddenFields, "Keys");
                CollectionAssert.Contains(hud.hiddenFields, "SpeedAccel");
                CollectionAssert.DoesNotContain(hud.hiddenFields, "MassFuel");
            }
            finally
            {
                Directory.Delete(destination, recursive: true);
            }
        });
    }

    [Test]
    public void TryCopyEditorSettingsJson_KeepsDiskHudWhenMemoryStoreIsStale()
    {
        WithRestoredEditorSettings(() =>
        {
            var diskData = new GameSettingsData();
            diskData.hud.language = "English";
            diskData.hud.background = "Dim";
            diskData.hud.hiddenFields = new[] { "Keys", "MissionHint", "Language" };
            File.WriteAllText(
                GameSettingsStore.FilePath,
                JsonUtility.ToJson(diskData, prettyPrint: true)
            );

            GameSettingsStore.ReloadFromDisk();
            FlightHudSettings.Unload();
            FlightDebugSettings.Unload();

            FlightHudField[] fields = (FlightHudField[])Enum.GetValues(typeof(FlightHudField));
            bool[] allVisible = new bool[fields.Length];
            for (int i = 0; i < allVisible.Length; i++)
            {
                allVisible[i] = true;
            }

            GameSettingsStore.WriteHudVisibility(allVisible);
            GameSettingsStore.WriteHudBackground(FlightHudBackground.Transparent);

            string destination = CreateTempDirectory("ShareBuildHudStale-");
            try
            {
                Assert.IsTrue(ShareBuildExporter.TryCopyEditorSettingsJson(destination));
                GameSettingsHud hud = ParseHud(
                    File.ReadAllText(Path.Combine(destination, GameSettingsStore.FileName))
                );
                Assert.AreEqual("English", hud.language);
                Assert.AreEqual("Dim", hud.background);
                CollectionAssert.AreEqual(
                    new[] { "Keys", "MissionHint", "Language" },
                    hud.hiddenFields
                );
            }
            finally
            {
                Directory.Delete(destination, recursive: true);
            }
        });
    }

    [Test]
    public void TryCopyEditorSettingsJson_FlushesLoadedDebugOverlaysIntoCopiedFile()
    {
        WithRestoredEditorSettings(() =>
        {
            FlightDebugSettings.Load();
            FlightDebugSettings.SetAllVisible(true);
            FlightDebugSettings.SetVisible(FlightDebugOverlay.Fps, false);
            FlightDebugSettings.SetVisible(FlightDebugOverlay.Camera, false);
            FlightDebugSettings.SetVisible(FlightDebugOverlay.Gfx, true);

            string destination = CreateTempDirectory("ShareBuildDebugFlush-");
            try
            {
                Assert.IsTrue(ShareBuildExporter.TryCopyEditorSettingsJson(destination));
                GameSettingsDebug debug = ParseDebug(
                    File.ReadAllText(Path.Combine(destination, GameSettingsStore.FileName))
                );
                CollectionAssert.Contains(debug.hiddenOverlays, "Fps");
                CollectionAssert.Contains(debug.hiddenOverlays, "Camera");
                CollectionAssert.DoesNotContain(debug.hiddenOverlays, "Gfx");
            }
            finally
            {
                Directory.Delete(destination, recursive: true);
            }
        });
    }

    static string CreateTempDirectory(string prefix)
    {
        string destination = Path.Combine(
            Path.GetTempPath(),
            prefix + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(destination);
        return destination;
    }

    static GameSettingsHud ParseHud(string json)
    {
        var parsed = new GameSettingsData();
        JsonUtility.FromJsonOverwrite(json, parsed);
        Assert.IsNotNull(parsed.hud);
        return parsed.hud;
    }

    static GameSettingsDebug ParseDebug(string json)
    {
        var parsed = new GameSettingsData();
        JsonUtility.FromJsonOverwrite(json, parsed);
        Assert.IsNotNull(parsed.debug);
        Assert.IsNotNull(parsed.debug.hiddenOverlays);
        return parsed.debug;
    }

    static void WithRestoredEditorSettings(Action body)
    {
        string path = GameSettingsStore.FilePath;
        bool existed = File.Exists(path);
        string original = existed ? File.ReadAllText(path) : null;

        try
        {
            body();
        }
        finally
        {
            if (existed)
            {
                File.WriteAllText(path, original);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }

            GameSettingsStore.ReloadFromDisk();
            FlightHudSettings.Unload();
            FlightDebugSettings.Unload();
            if (File.Exists(path))
            {
                FlightHudSettings.Reload();
                FlightDebugSettings.Reload();
            }
        }
    }

    [Test]
    public void ShouldSkipRelativePath_SkipsPlanetMapsAndKeepsShipsJson()
    {
        Assert.IsTrue(
            ShareBuildExporter.ShouldSkipRelativePath(
                "SpaceProgram_Data/StreamingAssets/Planet/earth.raw"
            )
        );
        Assert.IsTrue(
            ShareBuildExporter.ShouldSkipRelativePath(
                "SpaceProgram_Data/StreamingAssets/MapMode/clouds-alpha.png"
            )
        );
        Assert.IsTrue(
            ShareBuildExporter.ShouldSkipRelativePath(
                "SpaceProgram_Data/StreamingAssets/Content/Environment/RSS-Textures/EarthColor.dds"
            )
        );
        Assert.IsFalse(
            ShareBuildExporter.ShouldSkipRelativePath(
                "SpaceProgram_Data/StreamingAssets/Ships/test.json"
            )
        );
    }

    [Test]
    public void CopyHeavyPlayerData_UpdatesSkippedMapFilesOnly()
    {
        string sourceRoot = Path.Combine(
            Path.GetTempPath(),
            "HeavyBuildSrc-" + System.Guid.NewGuid().ToString("N")
        );
        string destRoot = Path.Combine(
            Path.GetTempPath(),
            "HeavyBuildDst-" + System.Guid.NewGuid().ToString("N")
        );

        try
        {
            Directory.CreateDirectory(Path.Combine(sourceRoot, "StreamingAssets", "Planet"));
            Directory.CreateDirectory(Path.Combine(sourceRoot, "StreamingAssets", "Ships"));
            Directory.CreateDirectory(
                Path.Combine(sourceRoot, "Content", "Environment", "RSS-Textures")
            );
            File.WriteAllText(
                Path.Combine(sourceRoot, "StreamingAssets", "Planet", "earth.raw"),
                "height"
            );
            File.WriteAllText(
                Path.Combine(sourceRoot, "StreamingAssets", "Ships", "craft.json"),
                "{}"
            );
            File.WriteAllText(
                Path.Combine(
                    sourceRoot,
                    "Content",
                    "Environment",
                    "RSS-Textures",
                    "EarthColor.dds"
                ),
                "dds"
            );

            int copied = HeavyBuildDataUpdater.CopyHeavyPlayerData(sourceRoot, destRoot);
            Assert.GreaterOrEqual(copied, 2);
            Assert.IsTrue(File.Exists(Path.Combine(destRoot, "Planet", "earth.raw")));
            Assert.IsTrue(
                File.Exists(
                    Path.Combine(destRoot, "Content", "Environment", "RSS-Textures", "EarthColor.dds")
                )
            );
            Assert.IsFalse(File.Exists(Path.Combine(destRoot, "Ships", "craft.json")));
        }
        finally
        {
            if (Directory.Exists(sourceRoot))
            {
                Directory.Delete(sourceRoot, recursive: true);
            }

            if (Directory.Exists(destRoot))
            {
                Directory.Delete(destRoot, recursive: true);
            }
        }
    }
}
