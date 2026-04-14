using UnityEngine;

public class SunVisual : MonoBehaviour
{
    public Vector3 sunWorldPosition = new Vector3(260.0f, 150.0f, 0.0f);
    public float sunSize = 5.0f;
    public float directionalLightIntensity = 3.2f;
    public float pointLightIntensity = 9.0f;
    public float pointLightRange = 180.0f;
    public Color sunColor = new Color(1.0f, 0.84f, 0.22f);
    public Color sunCoreColor = new Color(1.0f, 0.97f, 0.58f);

    GameObject sunObject;
    GameObject sunCoreObject;
    Light directionalLight;
    Light pointLight;
    Material sunCoreMaterial;

    public void Setup(Camera camera)
    {
        EnsureSunObject();
        DisableDefaultDirectionalLights();
        UpdateSunTransform();
    }

    void LateUpdate()
    {
        UpdateSunTransform();
    }

    void EnsureSunObject()
    {
        if (sunObject == null)
        {
            sunObject = new GameObject("Visual Sun");
        }

        sunCoreObject = EnsureSphereChild(sunObject, sunCoreObject, "Sun Core");
        RemoveOldGlowObject();

        Renderer coreRenderer = sunCoreObject.GetComponent<Renderer>();

        if (sunCoreMaterial == null)
        {
            sunCoreMaterial = CreateEmissiveMaterial(sunCoreColor, 4.0f);
        }

        coreRenderer.sharedMaterial = sunCoreMaterial;

        if (directionalLight == null)
        {
            directionalLight = sunObject.GetComponent<Light>();

            if (directionalLight == null)
            {
                directionalLight = sunObject.AddComponent<Light>();
            }
        }

        directionalLight.type = LightType.Directional;
        directionalLight.color = sunColor;
        directionalLight.intensity = directionalLightIntensity;
        directionalLight.shadows = LightShadows.None;

        Transform pointLightTransform = sunObject.transform.Find("Sun Point Light");

        if (pointLightTransform == null)
        {
            GameObject pointLightObject = new GameObject("Sun Point Light");
            pointLightObject.transform.SetParent(sunObject.transform);
            pointLightTransform = pointLightObject.transform;
        }

        pointLight = pointLightTransform.GetComponent<Light>();

        if (pointLight == null)
        {
            pointLight = pointLightTransform.gameObject.AddComponent<Light>();
        }

        pointLight.type = LightType.Point;
        pointLight.color = sunColor;
        pointLight.intensity = pointLightIntensity;
        pointLight.range = pointLightRange;
        pointLight.shadows = LightShadows.None;
    }

    GameObject EnsureSphereChild(
        GameObject parentObject,
        GameObject currentObject,
        string childName
    )
    {
        if (currentObject != null)
        {
            return currentObject;
        }

        Transform existingTransform = parentObject.transform.Find(childName);

        if (existingTransform != null)
        {
            return existingTransform.gameObject;
        }

        GameObject childObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        childObject.name = childName;
        childObject.transform.SetParent(parentObject.transform);

        Collider collider = childObject.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(collider);
        }

        return childObject;
    }

    void DisableDefaultDirectionalLights()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);

        for (int i = 0; i < lights.Length; i++)
        {
            if (
                lights[i] != null &&
                lights[i] != directionalLight &&
                lights[i].type == LightType.Directional
            )
            {
                lights[i].enabled = false;
            }
        }
    }

    Material CreateEmissiveMaterial(Color color, float emissionStrength)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        SetMaterialColor(material, color);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emissionStrength);
        }

        return material;
    }

    void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    void UpdateSunTransform()
    {
        if (sunObject == null)
        {
            return;
        }

        sunObject.transform.position = sunWorldPosition;
        sunObject.transform.rotation = Quaternion.identity;

        sunCoreObject.transform.localPosition = Vector3.zero;
        sunCoreObject.transform.localScale = Vector3.one * sunSize;

        pointLight.transform.localPosition = Vector3.zero;

        Vector3 lightDirection = (Vector3.zero - sunWorldPosition).normalized;

        if (lightDirection.sqrMagnitude <= 0.000001f)
        {
            lightDirection = Vector3.left;
        }

        directionalLight.transform.rotation =
            Quaternion.LookRotation(lightDirection, Vector3.up);
    }

    void RemoveOldGlowObject()
    {
        Transform oldGlowTransform = sunObject.transform.Find("Sun Glow");

        if (oldGlowTransform == null)
        {
            return;
        }

        Destroy(oldGlowTransform.gameObject);
    }
}
