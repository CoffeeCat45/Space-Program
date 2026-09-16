#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window: original OBJ + albedo/metallic/reflection PNGs → VAB container prefab.
/// </summary>
class CustomPartImporterWindow : EditorWindow
{
    const string PrefObjFolder = "SpaceProgram.CustomPart.LastObjFolder";
    const string PrefCategory = "SpaceProgram.CustomPart.Category";
    const string PrefReflectionKind = "SpaceProgram.CustomPart.ReflectionKind";
    const string PrefDryMass = "SpaceProgram.CustomPart.DryMass";
    const string PrefFuel = "SpaceProgram.CustomPart.Fuel";
    const string PrefThrust = "SpaceProgram.CustomPart.Thrust";
    const string PrefIsp = "SpaceProgram.CustomPart.Isp";

    string sourceObjPath = string.Empty;
    string albedoPath = string.Empty;
    string metallicPath = string.Empty;
    string reflectionPath = string.Empty;
    int categoryIndex = 2;
    int reflectionKindIndex;
    string partId = string.Empty;
    string displayName = string.Empty;
    float dryMass = 0.5f;
    float fuelCapacity;
    float maxThrustVac = 60f;
    float ispVacuum = 310f;
    bool preserveAuthoredMarkers = true;
    string statusMessage = string.Empty;
    MessageType statusType = MessageType.Info;
    Vector2 scroll;

    static readonly string[] Categories =
    {
        PartDatabase.CategoryCommand,
        PartDatabase.CategoryFuelTank,
        PartDatabase.CategoryEngine,
    };

    static readonly string[] ReflectionKindLabels =
    {
        "Шероховатость (Roughness) — как в Blender",
        "Блеск (Smoothness) — белое = зеркало",
    };

    [MenuItem("Space Program/Parts/Custom Part Importer", false, 20)]
    static void OpenWindow()
    {
        CustomPartImporterWindow window = GetWindow<CustomPartImporterWindow>("Custom Parts");
        window.minSize = new Vector2(560f, 640f);
        window.Show();
    }

    void OnEnable()
    {
        categoryIndex = Mathf.Clamp(EditorPrefs.GetInt(PrefCategory, 2), 0, Categories.Length - 1);
        reflectionKindIndex = Mathf.Clamp(EditorPrefs.GetInt(PrefReflectionKind, 0), 0, 1);

        if (EditorPrefs.HasKey(PrefDryMass))
        {
            dryMass = EditorPrefs.GetFloat(PrefDryMass, dryMass);
            fuelCapacity = EditorPrefs.GetFloat(PrefFuel, fuelCapacity);
            maxThrustVac = EditorPrefs.GetFloat(PrefThrust, maxThrustVac);
            ispVacuum = EditorPrefs.GetFloat(PrefIsp, ispVacuum);
        }
        else
        {
            ApplyCategoryDefaults(force: true);
        }
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Свои модели → prefab", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Не KSP / не ReStock.\n" +
            "Нужны: .obj (Y-up, метры) и 1–3 PNG: цвет, металлы, отражения/шероховатость.\n" +
            "Один атлас на несколько деталей — один материал: если PNG те же, новый .mat не создаётся.\n" +
            "Короткое имя: first-engine+tank-a. Если частей много — имя атласа, список деталей в Labels.\n" +
            "Карта отражений/шероховатости не красится поверх цвета: она идёт в Metallic Map (альфа = блеск).\n\n" +
            "Из Blender: Tools/Blender/export_unity_custom_part.py — оси Unity и копии карт рядом с OBJ.",
            MessageType.Info
        );

        EditorGUILayout.Space(6f);
        DrawPathRow("OBJ", ref sourceObjPath, "obj", OnObjPicked);
        DrawPathRow("Цвет (albedo)", ref albedoPath, "png", null);
        DrawPathRow("Металлы (optional)", ref metallicPath, "png", null);
        DrawPathRow("Отражения / roughness (optional)", ref reflectionPath, "png", null);

        EditorGUILayout.Space(4f);
        reflectionKindIndex = EditorGUILayout.Popup("Третья карта", reflectionKindIndex, ReflectionKindLabels);
        EditorPrefs.SetInt(PrefReflectionKind, reflectionKindIndex);
        EditorGUILayout.HelpBox(
            "Обычный выбор — шероховатость: чёрное блестит, белое матовое (Blender Roughness).\n" +
            "Блеск — только если карта уже наоборот: белое зеркало, чёрное матовое (как Unity Smoothness).\n" +
            "После смены пункта снова нажми «Собрать prefab».",
            MessageType.None
        );

        EditorGUILayout.Space(8f);
        int newCategory = EditorGUILayout.Popup("Категория", categoryIndex, Categories);

        if (newCategory != categoryIndex)
        {
            categoryIndex = newCategory;
            EditorPrefs.SetInt(PrefCategory, categoryIndex);
            ApplyCategoryDefaults(force: true);
        }

        partId = EditorGUILayout.TextField("Part id", partId);
        displayName = EditorGUILayout.TextField("Имя в VAB", displayName);

        EditorGUILayout.Space(4f);
        dryMass = EditorGUILayout.FloatField("Сухая масса, т", dryMass);
        fuelCapacity = EditorGUILayout.FloatField("Топливо (бак)", fuelCapacity);
        maxThrustVac = EditorGUILayout.FloatField("Тяга вакуум, кН", maxThrustVac);
        ispVacuum = EditorGUILayout.FloatField("Isp вакуум, с", ispVacuum);
        EditorPrefs.SetFloat(PrefDryMass, dryMass);
        EditorPrefs.SetFloat(PrefFuel, fuelCapacity);
        EditorPrefs.SetFloat(PrefThrust, maxThrustVac);
        EditorPrefs.SetFloat(PrefIsp, ispVacuum);

        preserveAuthoredMarkers = EditorGUILayout.ToggleLeft(
            "Не сбрасывать Node_Top / Node_Bottom / thrust при повторном импорте",
            preserveAuthoredMarkers
        );

        EditorGUILayout.Space(10f);

        if (GUILayout.Button("Собрать prefab", GUILayout.Height(36f)))
        {
            BuildPrefab();
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Открыть папку Custom"))
        {
            EnsureCustomRootVisible();
        }

        if (GUILayout.Button("Ping prefab") && !string.IsNullOrWhiteSpace(partId))
        {
            PingExistingPrefab();
        }

        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawPathRow(string label, ref string path, string extension, System.Action afterPick)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);

        EditorGUILayout.LabelField(
            string.IsNullOrWhiteSpace(path) ? "—" : Path.GetFileName(path),
            EditorStyles.textField
        );

        if (GUILayout.Button("…", GUILayout.Width(28f)))
        {
            string startFolder = EditorPrefs.GetString(PrefObjFolder, string.Empty);

            if (string.IsNullOrEmpty(startFolder) || !Directory.Exists(startFolder))
            {
                startFolder = string.IsNullOrWhiteSpace(path) ?
                    Application.dataPath :
                    Path.GetDirectoryName(path);
            }

            string picked = EditorUtility.OpenFilePanel(label, startFolder, extension);

            if (!string.IsNullOrEmpty(picked))
            {
                path = picked;
                EditorPrefs.SetString(PrefObjFolder, Path.GetDirectoryName(picked) ?? startFolder);
                afterPick?.Invoke();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    void OnObjPicked()
    {
        if (string.IsNullOrWhiteSpace(sourceObjPath) || !File.Exists(sourceObjPath))
        {
            return;
        }

        string stem = Path.GetFileNameWithoutExtension(sourceObjPath);
        partId = CustomPartImportPipeline.SanitizePartId(stem);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = PartCatalogHelpers.HumanizePartId(partId);
        }

        CustomPartImportPipeline.DetectMapsInFolder(
            sourceObjPath,
            out string detectedAlbedo,
            out string detectedMetallic,
            out string detectedReflection
        );

        if (!string.IsNullOrEmpty(detectedAlbedo))
        {
            albedoPath = detectedAlbedo;
        }

        if (!string.IsNullOrEmpty(detectedMetallic))
        {
            metallicPath = detectedMetallic;
        }

        if (!string.IsNullOrEmpty(detectedReflection))
        {
            reflectionPath = detectedReflection;
        }

        statusMessage =
            "Карты рядом с OBJ: цвет=" + FileNameOrDash(albedoPath) +
            ", металлы=" + FileNameOrDash(metallicPath) +
            ", отражения=" + FileNameOrDash(reflectionPath);
        statusType = MessageType.Info;
        Repaint();
    }

    static string FileNameOrDash(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? "нет" : Path.GetFileName(path);
    }

    void ApplyCategoryDefaults(bool force)
    {
        string category = Categories[categoryIndex];

        if (!force && dryMass > 0f)
        {
            return;
        }

        if (category == PartDatabase.CategoryFuelTank)
        {
            dryMass = 0.5f;
            fuelCapacity = 400f;
            maxThrustVac = 0f;
            ispVacuum = 0f;
            PersistStats();
            return;
        }

        if (category == PartDatabase.CategoryCommand)
        {
            dryMass = 1f;
            fuelCapacity = 0f;
            maxThrustVac = 0f;
            ispVacuum = 0f;
            PersistStats();
            return;
        }

        dryMass = 0.5f;
        fuelCapacity = 0f;
        maxThrustVac = 60f;
        ispVacuum = 310f;
        PersistStats();
    }

    void PersistStats()
    {
        EditorPrefs.SetFloat(PrefDryMass, dryMass);
        EditorPrefs.SetFloat(PrefFuel, fuelCapacity);
        EditorPrefs.SetFloat(PrefThrust, maxThrustVac);
        EditorPrefs.SetFloat(PrefIsp, ispVacuum);
    }

    void BuildPrefab()
    {
        CustomPartImportRequest request = new CustomPartImportRequest
        {
            SourceObjFullPath = sourceObjPath,
            AlbedoFullPath = albedoPath,
            MetallicFullPath = metallicPath,
            ReflectionFullPath = reflectionPath,
            ReflectionKind = reflectionKindIndex == 0 ?
                CustomPartReflectionKind.Roughness :
                CustomPartReflectionKind.Smoothness,
            Category = Categories[categoryIndex],
            PartId = partId,
            DisplayName = displayName,
            DryMass = dryMass,
            FuelCapacity = fuelCapacity,
            MaxThrustVac = maxThrustVac,
            IspVacuum = ispVacuum,
            PreserveAuthoredMarkers = preserveAuthoredMarkers,
        };

        CustomPartImportResult result = CustomPartImportPipeline.Import(request);
        statusMessage = result.Message;
        statusType = result.Success ? MessageType.Info : MessageType.Error;

        if (!result.Success)
        {
            Debug.LogError("Custom part import: " + result.Message);
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabAssetPath);

        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
    }

    static void EnsureCustomRootVisible()
    {
        if (!AssetDatabase.IsValidFolder(CustomPartImportPipeline.CustomMeshesAssetRoot))
        {
            string partsRoot = "Assets/Models/Parts";

            if (AssetDatabase.IsValidFolder(partsRoot))
            {
                AssetDatabase.CreateFolder(partsRoot, "Custom");
            }
        }

        DefaultAsset folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(
            CustomPartImportPipeline.CustomMeshesAssetRoot
        );

        if (folder != null)
        {
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }
        else
        {
            EditorUtility.RevealInFinder(Application.dataPath);
        }
    }

    void PingExistingPrefab()
    {
        string category = Categories[categoryIndex];
        string id = CustomPartImportPipeline.SanitizePartId(partId);
        string prefabPath = ReStockPartPaths.GetContainerPrefabAssetPath(category, id);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            statusMessage = "Prefab ещё нет: " + prefabPath;
            statusType = MessageType.Warning;
            return;
        }

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }
}
#endif
