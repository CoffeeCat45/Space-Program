using UnityEngine;

static class ExplosionEffects
{
    static Material cachedParticleMaterial;

    public static void PlayBlockImpact(
        Vector3 worldPosition,
        float sizeScale,
        float worldScale
    )
    {
        float scale = Mathf.Max(0.35f, sizeScale);
        GameObject root = CreateBurstRoot(worldPosition, "BlockImpactBurst");
        root.transform.localScale = Vector3.one * scale;

        ParticleSystem fire = CreateParticleSystem(root.transform, "Fire");
        ConfigureFireBurst(
            fire,
            duration: 0.35f,
            maxParticles: 48,
            burstCount: 24,
            shapeRadius: 0.08f,
            minSpeed: 1.4f * worldScale,
            maxSpeed: 3.8f * worldScale,
            minSize: 0.06f,
            maxSize: 0.18f,
            minLifetime: 0.18f,
            maxLifetime: 0.55f,
            hotColor: new Color(1.0f, 0.55f, 0.12f, 1.0f),
            coolColor: new Color(1.0f, 0.92f, 0.35f, 0.85f)
        );
        PlayAndDestroy(root, 1.5f);
    }

    public static void PlayStageSeparation(Vector3 worldPosition, float worldScale)
    {
        float scale = Mathf.Max(0.8f, worldScale * 16.0f);
        GameObject root = CreateBurstRoot(worldPosition, "StageSeparationBurst");
        root.transform.localScale = Vector3.one * scale;

        ParticleSystem fire = CreateParticleSystem(root.transform, "Flash");
        ConfigureFireBurst(
            fire,
            duration: 0.55f,
            maxParticles: 64,
            burstCount: 36,
            shapeRadius: 0.22f,
            minSpeed: 2.0f * worldScale,
            maxSpeed: 5.5f * worldScale,
            minSize: 0.10f,
            maxSize: 0.28f,
            minLifetime: 0.22f,
            maxLifetime: 0.72f,
            hotColor: new Color(1.0f, 0.72f, 0.18f, 1.0f),
            coolColor: new Color(1.0f, 0.95f, 0.55f, 0.75f)
        );

        ParticleSystem sparks = CreateParticleSystem(root.transform, "Sparks");
        ConfigureFireBurst(
            sparks,
            duration: 0.45f,
            maxParticles: 40,
            burstCount: 28,
            shapeRadius: 0.14f,
            minSpeed: 3.5f * worldScale,
            maxSpeed: 8.0f * worldScale,
            minSize: 0.03f,
            maxSize: 0.08f,
            minLifetime: 0.12f,
            maxLifetime: 0.42f,
            hotColor: new Color(1.0f, 0.85f, 0.35f, 1.0f),
            coolColor: new Color(1.0f, 0.55f, 0.12f, 0.6f)
        );

        ParticleSystem.MainModule sparkMain = sparks.main;
        sparkMain.gravityModifier = 0.35f;

        PlayAndDestroy(root, 2.0f);
    }

    public static void PlayCrash(Vector3 worldPosition, float worldScale)
    {
        float scale = Mathf.Max(1.4f, worldScale * 28.0f);
        GameObject root = CreateBurstRoot(worldPosition, "CrashBurst");
        root.transform.localScale = Vector3.one * scale;

        ParticleSystem fire = CreateParticleSystem(root.transform, "Fireball");
        ConfigureFireBurst(
            fire,
            duration: 0.9f,
            maxParticles: 96,
            burstCount: 56,
            shapeRadius: 0.35f,
            minSpeed: 2.5f * worldScale,
            maxSpeed: 7.5f * worldScale,
            minSize: 0.12f,
            maxSize: 0.36f,
            minLifetime: 0.28f,
            maxLifetime: 0.95f,
            hotColor: new Color(1.0f, 0.35f, 0.05f, 1.0f),
            coolColor: new Color(1.0f, 0.65f, 0.12f, 0.85f)
        );

        ParticleSystem smoke = CreateParticleSystem(root.transform, "Smoke");
        ConfigureFireBurst(
            smoke,
            duration: 1.2f,
            maxParticles: 72,
            burstCount: 40,
            shapeRadius: 0.28f,
            minSpeed: 0.8f * worldScale,
            maxSpeed: 2.8f * worldScale,
            minSize: 0.18f,
            maxSize: 0.42f,
            minLifetime: 0.55f,
            maxLifetime: 1.35f,
            hotColor: new Color(0.35f, 0.35f, 0.35f, 0.75f),
            coolColor: new Color(0.18f, 0.18f, 0.18f, 0.35f)
        );

        ParticleSystem.MainModule smokeMain = smoke.main;
        smokeMain.gravityModifier = -0.08f;

        PlayAndDestroy(root, 2.8f);
    }

    public static void PlayDockingCapture(Vector3 worldPosition, float worldScale)
    {
        float scale = Mathf.Max(0.55f, worldScale * 10.0f);
        GameObject root = CreateBurstRoot(worldPosition, "DockingCaptureBurst");
        root.transform.localScale = Vector3.one * scale;

        ParticleSystem flash = CreateParticleSystem(root.transform, "Flash");
        ConfigureFireBurst(
            flash,
            duration: 0.4f,
            maxParticles: 32,
            burstCount: 18,
            shapeRadius: 0.12f,
            minSpeed: 0.8f * worldScale,
            maxSpeed: 2.2f * worldScale,
            minSize: 0.05f,
            maxSize: 0.14f,
            minLifetime: 0.15f,
            maxLifetime: 0.45f,
            hotColor: new Color(0.35f, 1.0f, 0.85f, 1.0f),
            coolColor: new Color(0.85f, 1.0f, 0.55f, 0.8f)
        );

        PlayAndDestroy(root, 1.2f);
    }

    static GameObject CreateBurstRoot(Vector3 worldPosition, string name)
    {
        GameObject root = new GameObject(name);
        root.transform.position = worldPosition;
        root.transform.rotation = Quaternion.identity;
        return root;
    }

    static ParticleSystem CreateParticleSystem(Transform parent, string name)
    {
        GameObject particleObject = new GameObject(name);
        particleObject.transform.SetParent(parent, false);
        particleObject.transform.localPosition = Vector3.zero;
        particleObject.transform.localRotation = Quaternion.identity;
        particleObject.transform.localScale = Vector3.one;

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    static void ConfigureFireBurst(
        ParticleSystem particles,
        float duration,
        int maxParticles,
        int burstCount,
        float shapeRadius,
        float minSpeed,
        float maxSpeed,
        float minSize,
        float maxSize,
        float minLifetime,
        float maxLifetime,
        Color hotColor,
        Color coolColor
    )
    {
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = duration;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(hotColor, coolColor);
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0.0f;
        emission.SetBursts(
            new[]
            {
                new ParticleSystem.Burst(0.0f, (short)burstCount)
            }
        );

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = shapeRadius;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetParticleMaterial();
    }

    static void PlayAndDestroy(GameObject root, float destroyDelay)
    {
        ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>();

        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Play();
        }

        Object.Destroy(root, destroyDelay);
    }

    static Material GetParticleMaterial()
    {
        if (cachedParticleMaterial != null)
        {
            return cachedParticleMaterial;
        }

        cachedParticleMaterial = RuntimeMaterialFactory.CreateParticleMaterial(Color.white);
        return cachedParticleMaterial;
    }
}
