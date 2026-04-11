using UnityEngine;

static class BodyView
{
    public static GameObject Create(string bodyName)
    {
        GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bodyObject.name = bodyName;

        if (bodyName == "Planet")
        {
            bodyObject.transform.localScale = Vector3.one * 1.6f;
        }
        else if (bodyName == "Moon")
        {
            bodyObject.transform.localScale = Vector3.one * 0.8f;
        }
        else
        {
            bodyObject.transform.localScale = Vector3.one * 0.35f;
        }

        return bodyObject;
    }

    public static void Update(GameObject bodyObject, BodyState bodyState, float worldScale)
    {
        bodyObject.transform.position = new Vector3(
            (float)(bodyState.position.x * worldScale),
            (float)(bodyState.position.y * worldScale),
            0
        );

        Renderer renderer = bodyObject.GetComponent<Renderer>();

        if (renderer == null)
        {
            return;
        }

        if (bodyState.name == "Planet")
        {
            renderer.material.color = new Color(0.2f, 0.6f, 1.0f);
        }
        else if (bodyState.name == "Moon")
        {
            renderer.material.color = new Color(0.8f, 0.8f, 0.8f);
        }
        else if (bodyState.isActivePrimary)
        {
            renderer.material.color = new Color(0.3f, 1.0f, 0.3f);
        }
        else
        {
            renderer.material.color = new Color(1.0f, 0.8f, 0.2f);
        }
    }
}
