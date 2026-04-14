using UnityEngine;

static class BodyView
{
    const float VelocityVectorLength = 1.6f;
    const float ThrustDirectionLength = 1.1f;
    const float PlannedDeltaVVectorScale = 0.8f;
    const float MinEngineParticleRate = 18.0f;
    const float MaxEngineParticleRate = 180.0f;

    public static GameObject Create(string bodyName)
    {
        if (bodyName == "Ship")
        {
            return CreateShip(bodyName);
        }

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

    static GameObject CreateShip(string bodyName)
    {
        GameObject shipObject = new GameObject(bodyName);

        GameObject hullObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        hullObject.name = "Hull";
        hullObject.transform.SetParent(shipObject.transform);
        hullObject.transform.localPosition = Vector3.zero;
        hullObject.transform.localRotation = Quaternion.identity;
        hullObject.transform.localScale = new Vector3(0.25f, 0.45f, 0.25f);

        GameObject noseObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        noseObject.name = "Nose";
        noseObject.transform.SetParent(shipObject.transform);
        noseObject.transform.localPosition = new Vector3(0, 0.55f, 0);
        noseObject.transform.localRotation = Quaternion.identity;
        noseObject.transform.localScale = Vector3.one * 0.18f;

        CreateVectorLine(shipObject, "VelocityVector", new Color(0.1f, 1.0f, 0.2f));
        CreateVectorLine(shipObject, "ThrustDirection", new Color(1.0f, 0.55f, 0.1f));
        CreateVectorLine(shipObject, "PlannedDeltaV", new Color(1.0f, 0.85f, 0.0f));
        CreateEngineExhaust(shipObject);
        shipObject.AddComponent<ShipTrailView>();

        return shipObject;
    }

    static void CreateVectorLine(GameObject parentObject, string lineName, Color color)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(parentObject.transform);

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.04f;
        lineRenderer.endWidth = 0.04f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

    static void CreateEngineExhaust(GameObject shipObject)
    {
        GameObject exhaustObject = new GameObject("EngineExhaust");
        exhaustObject.transform.SetParent(shipObject.transform);
        exhaustObject.transform.localPosition = new Vector3(0, -0.58f, 0);
        exhaustObject.transform.localRotation = Quaternion.Euler(90.0f, 0, 0);
        exhaustObject.transform.localScale = Vector3.one;

        ParticleSystem particles = exhaustObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1.0f, 0.45f, 0.08f, 0.95f),
            new Color(1.0f, 0.95f, 0.35f, 0.75f)
        );
        main.maxParticles = 260;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        emission.rateOverTime = 0.0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 13.0f;
        shape.radius = 0.04f;
        shape.length = 0.16f;

        ParticleSystemRenderer particleRenderer =
            particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.material = CreateParticleMaterial();
    }

    public static void Update(GameObject bodyObject, BodyState bodyState, float worldScale)
    {
        bodyObject.transform.position = new Vector3(
            (float)(bodyState.position.x * worldScale),
            (float)(bodyState.position.y * worldScale),
            0
        );

        if (bodyState.name == "Planet")
        {
            SetColor(bodyObject, new Color(0.2f, 0.6f, 1.0f));
        }
        else if (bodyState.name == "Moon")
        {
            SetColor(bodyObject, new Color(0.8f, 0.8f, 0.8f));
        }
        else if (bodyState.isActivePrimary)
        {
            SetColor(bodyObject, new Color(0.3f, 1.0f, 0.3f));
        }
        else
        {
            SetColor(bodyObject, new Color(1.0f, 0.8f, 0.2f));
        }

        if (bodyState.name == "Ship")
        {
            UpdateVelocityVector(bodyObject, bodyState, worldScale);
            UpdateShipTrail(bodyObject);
        }
    }

    public static void UpdateThrustDirection(GameObject shipObject, float shipRotationDegrees)
    {
        LineRenderer lineRenderer = GetVectorLine(shipObject, "ThrustDirection");

        if (lineRenderer == null)
        {
            return;
        }

        float rotationRadians = shipRotationDegrees * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(
            Mathf.Cos(rotationRadians),
            Mathf.Sin(rotationRadians),
            0
        );

        Vector3 startPosition = shipObject.transform.position;
        Vector3 endPosition = startPosition + direction * ThrustDirectionLength;

        lineRenderer.SetPosition(0, startPosition);
        lineRenderer.SetPosition(1, endPosition);
    }

    public static void UpdatePlannedDeltaV(
        GameObject shipObject,
        global::Vector2 plannedDeltaV
    )
    {
        LineRenderer lineRenderer = GetVectorLine(shipObject, "PlannedDeltaV");

        if (lineRenderer == null)
        {
            return;
        }

        Vector3 startPosition = shipObject.transform.position;

        if (plannedDeltaV.LengthSquared() == 0)
        {
            lineRenderer.SetPosition(0, startPosition);
            lineRenderer.SetPosition(1, startPosition);
            return;
        }

        Vector3 direction = new Vector3(
            (float)plannedDeltaV.x,
            (float)plannedDeltaV.y,
            0
        );
        Vector3 endPosition =
            startPosition + direction.normalized * Mathf.Max(
                0.4f,
                direction.magnitude * PlannedDeltaVVectorScale
            );

        lineRenderer.SetPosition(0, startPosition);
        lineRenderer.SetPosition(1, endPosition);
    }

    public static void UpdateEngineExhaust(
        GameObject shipObject,
        float throttle,
        bool engineFiring
    )
    {
        ParticleSystem particles = GetEngineExhaust(shipObject);

        if (particles == null)
        {
            return;
        }

        throttle = Mathf.Clamp01(throttle);
        bool shouldEmit = engineFiring && throttle > 0.001f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = shouldEmit;
        emission.rateOverTime = shouldEmit ?
            Mathf.Lerp(MinEngineParticleRate, MaxEngineParticleRate, throttle) :
            0.0f;

        ParticleSystem.MainModule main = particles.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            Mathf.Lerp(0.7f, 1.6f, throttle),
            Mathf.Lerp(1.4f, 4.2f, throttle)
        );
        main.startSize = new ParticleSystem.MinMaxCurve(
            Mathf.Lerp(0.035f, 0.07f, throttle),
            Mathf.Lerp(0.08f, 0.20f, throttle)
        );

        if (shouldEmit && !particles.isPlaying)
        {
            particles.Play();
        }
        else if (!shouldEmit && particles.isPlaying)
        {
            particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    static void UpdateVelocityVector(GameObject shipObject, BodyState bodyState, float worldScale)
    {
        LineRenderer lineRenderer = GetVectorLine(shipObject, "VelocityVector");

        if (lineRenderer == null)
        {
            return;
        }

        Vector3 startPosition = shipObject.transform.position;
        Vector3 velocity = new Vector3(
            (float)bodyState.velocity.x,
            (float)bodyState.velocity.y,
            0
        );

        if (velocity.sqrMagnitude == 0)
        {
            lineRenderer.SetPosition(0, startPosition);
            lineRenderer.SetPosition(1, startPosition);
            return;
        }

        Vector3 endPosition = startPosition + velocity.normalized * VelocityVectorLength;

        lineRenderer.SetPosition(0, startPosition);
        lineRenderer.SetPosition(1, endPosition);
    }

    static LineRenderer GetVectorLine(GameObject bodyObject, string lineName)
    {
        Transform lineTransform = bodyObject.transform.Find(lineName);

        if (lineTransform == null)
        {
            return null;
        }

        return lineTransform.GetComponent<LineRenderer>();
    }

    static ParticleSystem GetEngineExhaust(GameObject bodyObject)
    {
        Transform exhaustTransform = bodyObject.transform.Find("EngineExhaust");

        if (exhaustTransform == null)
        {
            return null;
        }

        return exhaustTransform.GetComponent<ParticleSystem>();
    }

    static void UpdateShipTrail(GameObject shipObject)
    {
        ShipTrailView shipTrailView = shipObject.GetComponent<ShipTrailView>();

        if (shipTrailView == null)
        {
            return;
        }

        shipTrailView.AddPoint(shipObject.transform.position);
    }

    static void SetColor(GameObject bodyObject, Color color)
    {
        Renderer[] renderers = bodyObject.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is LineRenderer)
            {
                continue;
            }

            renderers[i].material.color = color;
        }
    }

    static Material CreateParticleMaterial()
    {
        Shader shader = Shader.Find("Particles/Standard Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        return new Material(shader);
    }
}
