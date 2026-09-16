using UnityEngine;

/// <summary>
/// Builds the flight chunk material with RSS-Terrain biome splat textures.
/// </summary>
static class PlanetBiomeTerrainMaterialBuilder
{
    public static Material CreateChunkMaterial(
        Texture2D earthColorMap,
        PlanetParallaxTerrainSet terrainSet
    )
    {
        Material material = SpaceProgramShaders.CreateMaterial(
            "SpaceProgram/EarthPlanetLocalChunks"
        );

        if (material == null)
        {
            Debug.LogError(
                "PlanetBiomeTerrainMaterialBuilder: EarthPlanetLocalChunks shader missing."
            );
            return null;
        }

        material.name = "PlanetLocalChunks";

        bool hasDetail = ApplyTerrainTextures(material, terrainSet);
        ApplyMacroColor(material, earthColorMap);
        ApplyTerrainConstants(material, hasDetail || HasUsableDetailSet(terrainSet));
        LogMaterialState(material, terrainSet, hasDetail);
        return material;
    }

    public static bool HasCompleteTerrainSet(PlanetParallaxTerrainSet terrainSet)
    {
        return terrainSet != null &&
            terrainSet.InfluenceMap != null &&
            terrainSet.MidAlbedo != null;
    }

    public static bool HasPartialTerrainSet(PlanetParallaxTerrainSet terrainSet)
    {
        return terrainSet != null && terrainSet.HasAnyAlbedo;
    }

    public static bool HasUsableDetailSet(PlanetParallaxTerrainSet terrainSet)
    {
        if (HasCompleteTerrainSet(terrainSet))
        {
            return true;
        }

        return CelestialTerrainRuntime.IsMoon && HasPartialTerrainSet(terrainSet);
    }

    static bool ApplyTerrainTextures(Material material, PlanetParallaxTerrainSet terrainSet)
    {
        if (terrainSet == null)
        {
            return false;
        }

        PlanetParallaxTextures.ApplyTerrainSetToMaterial(material, terrainSet);
        return HasUsableDetailSet(terrainSet);
    }

    static void ApplyMacroColor(Material material, Texture2D earthColorMap)
    {
        if (!material.HasProperty("_PlanetColorMap"))
        {
            return;
        }

        if (earthColorMap != null)
        {
            PlanetParallaxTextures.ConfigureTextureSampling(earthColorMap, clampWrap: true);
            material.SetTexture("_PlanetColorMap", earthColorMap);
            if (CelestialTerrainRuntime.IsMoon)
            {
                PlanetTerrainTextureResources.ApplyMoonColorShaderProperties(material);
            }
            else
            {
                PlanetTerrainTextureResources.ApplyEarthColorShaderProperties(material);
            }
            return;
        }

        material.SetTexture("_PlanetColorMap", Texture2D.whiteTexture);
    }

    static void ApplyTerrainConstants(Material material, bool hasDetail)
    {
        float heightScale = PlanetSurfaceConstants.UnityVisualHeightScale;

        if (material.HasProperty("_PlanetRadius"))
        {
            material.SetFloat("_PlanetRadius", CelestialTerrainRuntime.LodBodyRadiusUnity);
        }

        if (material.HasProperty("_VisualHeightScale"))
        {
            material.SetFloat("_VisualHeightScale", heightScale);
        }

        if (material.HasProperty("_DetailMetersPerTile"))
        {
            material.SetFloat("_DetailMetersPerTile", PlanetBiomeTerrainConfig.DetailMetersPerTile);
        }

        if (material.HasProperty("_DetailFarMetersPerTile"))
        {
            material.SetFloat(
                "_DetailFarMetersPerTile",
                PlanetBiomeTerrainConfig.DetailFarMetersPerTile
            );
        }

        if (material.HasProperty("_DetailNearFadeMeters"))
        {
            material.SetFloat(
                "_DetailNearFadeMeters",
                PlanetBiomeTerrainConfig.DetailNearFadeMeters
            );
        }

        if (material.HasProperty("_DetailFarFadeMeters"))
        {
            material.SetFloat(
                "_DetailFarFadeMeters",
                PlanetBiomeTerrainConfig.DetailFarFadeMeters
            );
        }

        if (material.HasProperty("_RealEarthRadius"))
        {
            material.SetFloat(
                "_RealEarthRadius",
                CelestialTerrainRuntime.LodBodyRealRadiusMeters
            );
        }

        if (material.HasProperty("_SurfaceTextureScale"))
        {
            material.SetFloat("_SurfaceTextureScale", PlanetBiomeTerrainConfig.SurfaceTextureScale);
        }

        bool heightSlopeSplat = !CelestialTerrainRuntime.HasOcean;

        if (material.HasProperty("_LowAltitudeStart"))
        {
            material.SetFloat(
                "_LowAltitudeStart",
                heightSlopeSplat ?
                    PlanetBiomeTerrainConfig.HeightSlopeLowStartMeters :
                    PlanetBiomeTerrainConfig.LowAltitudeStartMeters * heightScale
            );
        }

        if (material.HasProperty("_LowAltitudeEnd"))
        {
            material.SetFloat(
                "_LowAltitudeEnd",
                heightSlopeSplat ?
                    PlanetBiomeTerrainConfig.HeightSlopeLowEndMeters :
                    PlanetBiomeTerrainConfig.LowAltitudeEndMeters * heightScale
            );
        }

        if (material.HasProperty("_HighAltitudeStart"))
        {
            material.SetFloat(
                "_HighAltitudeStart",
                heightSlopeSplat ?
                    PlanetBiomeTerrainConfig.HeightSlopeHighStartMeters :
                    PlanetBiomeTerrainConfig.HighAltitudeStartMeters * heightScale
            );
        }

        if (material.HasProperty("_HighAltitudeEnd"))
        {
            material.SetFloat(
                "_HighAltitudeEnd",
                heightSlopeSplat ?
                    PlanetBiomeTerrainConfig.HeightSlopeHighEndMeters :
                    PlanetBiomeTerrainConfig.HighAltitudeEndMeters * heightScale
            );
        }

        if (material.HasProperty("_SteepPower"))
        {
            material.SetFloat(
                "_SteepPower",
                heightSlopeSplat ?
                    PlanetBiomeTerrainConfig.HeightSlopeSteepPower :
                    PlanetBiomeTerrainConfig.SteepPower
            );
        }

        if (material.HasProperty("_SteepContrast"))
        {
            material.SetFloat(
                "_SteepContrast",
                heightSlopeSplat ?
                    PlanetBiomeTerrainConfig.HeightSlopeSteepContrast :
                    PlanetBiomeTerrainConfig.SteepContrast
            );
        }

        if (material.HasProperty("_SteepMidpoint"))
        {
            material.SetFloat(
                "_SteepMidpoint",
                heightSlopeSplat ?
                    PlanetBiomeTerrainConfig.HeightSlopeSteepMidpoint :
                    PlanetBiomeTerrainConfig.SteepMidpoint
            );
        }

        if (material.HasProperty("_SteepSlopeThreshold"))
        {
            material.SetFloat("_SteepSlopeThreshold", PlanetBiomeTerrainConfig.SteepSlopeThreshold);
        }

        if (material.HasProperty("_MacroBlendStart"))
        {
            material.SetFloat(
                "_MacroBlendStart",
                CelestialTerrainRuntime.IsMoon ?
                    PlanetBiomeTerrainConfig.MoonMacroBlendStartRealMeters :
                    PlanetBiomeTerrainConfig.MacroSatelliteBlendStartMeters
            );
        }

        if (material.HasProperty("_MacroBlendEnd"))
        {
            material.SetFloat(
                "_MacroBlendEnd",
                CelestialTerrainRuntime.IsMoon ?
                    PlanetBiomeTerrainConfig.MoonMacroBlendEndRealMeters :
                    PlanetBiomeTerrainConfig.MacroSatelliteBlendEndMeters
            );
        }

        if (material.HasProperty("_MacroBlendMax"))
        {
            material.SetFloat(
                "_MacroBlendMax",
                CelestialTerrainRuntime.IsMoon ?
                    PlanetBiomeTerrainConfig.MoonMacroBlendMax :
                    PlanetBiomeTerrainConfig.MacroSatelliteMaxBlend
            );
        }

        if (material.HasProperty("_GroundAmbientFloor"))
        {
            material.SetFloat("_GroundAmbientFloor", PlanetBiomeTerrainConfig.GroundAmbientFloor);
        }

        if (material.HasProperty("_SeaLevelHeightMeters"))
        {
            material.SetFloat("_SeaLevelHeightMeters", 0f);
        }

        if (material.HasProperty("_ShallowWaterDepthMeters"))
        {
            material.SetFloat("_ShallowWaterDepthMeters", 12f);
        }

        if (material.HasProperty("_NormalStrength"))
        {
            material.SetFloat(
                "_NormalStrength",
                hasDetail ? PlanetBiomeTerrainConfig.NormalStrength : 0f
            );
        }

        if (material.HasProperty("_DetailActive"))
        {
            material.SetFloat("_DetailActive", hasDetail ? 1f : 0f);
        }

        if (material.HasProperty("_SurfaceBlendMode"))
        {
            material.SetFloat("_SurfaceBlendMode", heightSlopeSplat ? 1f : 0f);
        }

        if (material.HasProperty("_HasOcean"))
        {
            material.SetFloat("_HasOcean", CelestialTerrainRuntime.HasOcean ? 1f : 0f);
        }

        if (material.HasProperty("_WaveMetersPerTile"))
        {
            material.SetFloat("_WaveMetersPerTile", PlanetBiomeTerrainConfig.WaveMetersPerTile);
        }

        if (material.HasProperty("_WaveSpeed"))
        {
            material.SetFloat("_WaveSpeed", PlanetBiomeTerrainConfig.WaveSpeed);
        }

        if (material.HasProperty("_WaveStrength"))
        {
            material.SetFloat("_WaveStrength", PlanetBiomeTerrainConfig.WaveStrength);
        }

        if (material.HasProperty("_WaveHeightMeters"))
        {
            material.SetFloat(
                "_WaveHeightMeters",
                PlanetBiomeTerrainConfig.WaveHeightMetersForBody(
                    CelestialTerrainRuntime.HasOcean
                )
            );
        }

        if (material.HasProperty("_OceanDepthPackMeters"))
        {
            material.SetFloat(
                "_OceanDepthPackMeters",
                PlanetBiomeTerrainConfig.OceanDepthPackMeters
            );
        }

        if (material.HasProperty("_OceanSpecular"))
        {
            material.SetFloat("_OceanSpecular", PlanetBiomeTerrainConfig.OceanSpecular);
        }

        if (material.HasProperty("_OceanColor"))
        {
            material.SetColor("_OceanColor", new Color(0.02f, 0.10f, 0.20f, 1f));
        }

        if (material.HasProperty("_OceanSatelliteBlend"))
        {
            material.SetFloat(
                "_OceanSatelliteBlend",
                PlanetBiomeTerrainConfig.OceanSatelliteBlend
            );
        }

        if (material.HasProperty("_FallbackColor"))
        {
            material.SetColor(
                "_FallbackColor",
                heightSlopeSplat ?
                    new Color(0.38f, 0.37f, 0.35f, 1f) :
                    new Color(0.28f, 0.48f, 0.22f, 1f)
            );
        }
    }

    static void LogMaterialState(
        Material material,
        PlanetParallaxTerrainSet terrainSet,
        bool hasDetail
    )
    {
        if (hasDetail || HasUsableDetailSet(terrainSet))
        {
            return;
        }

        if (terrainSet == null || PlanetTerrainPipelineSettings.UseGebcoOnlyPipeline)
        {
            return;
        }

        RssTerrainSplatPack pack = RssTerrainSplatPack.ForLodBody();
        Debug.LogError(
            "PlanetBiomeTerrainMaterialBuilder: biome textures missing. " +
            "Place " + pack.ContentBodyFolderName + "_*.dds in " +
            pack.ContentAssetFolder +
            " or " +
            pack.ResolveLegacyPluginDataDirectory() +
            ", then run Space Program > Environment > Configure RSS-Terrain Earth Textures."
        );

        if (terrainSet != null)
        {
            Debug.LogWarning(
                "PlanetBiomeTerrainMaterialBuilder: influence=" +
                (terrainSet.InfluenceMap != null) +
                " low=" + (terrainSet.LowAlbedo != null) +
                " mid=" + (terrainSet.MidAlbedo != null) +
                " high=" + (terrainSet.HighAlbedo != null) +
                " steep=" + (terrainSet.SteepAlbedo != null) +
                "."
            );
        }
    }
}
