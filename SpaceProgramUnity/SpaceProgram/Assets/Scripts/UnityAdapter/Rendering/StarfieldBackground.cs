using UnityEngine;

public class StarfieldBackground : MonoBehaviour
{
    public int starCount = 2800;
    public float starRadius = 700.0f;
    public float minStarSize = 0.025f;
    public float maxStarSize = 0.28f;
    public int seed = 42;
    public Color backgroundColor = new Color(0.005f, 0.006f, 0.014f);

    ParticleSystem starParticles;

    public void Setup(Camera targetCamera)
    {
        if (GalaxySkyboxEnvironment.TryApply(targetCamera))
        {
            if (starParticles != null)
            {
                starParticles.Stop();
            }

            return;
        }

        if (targetCamera != null)
        {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = backgroundColor;
        }

        BuildStarfield();
    }

    void BuildStarfield()
    {
        starParticles = GetComponent<ParticleSystem>();

        if (starParticles == null)
        {
            starParticles = gameObject.AddComponent<ParticleSystem>();
        }

        ParticleSystem.MainModule main = starParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = starCount;

        ParticleSystem.EmissionModule emission = starParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = starParticles.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer =
            starParticles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.material = CreateStarMaterial();

        ParticleSystem.Particle[] particles =
            new ParticleSystem.Particle[starCount];
        System.Random random = new System.Random(seed);

        for (int i = 0; i < particles.Length; i++)
        {
            Vector3 direction = RandomDirection(random);
            float radius = starRadius * RandomRange(random, 0.65f, 1.0f);
            float brightness = RandomRange(random, 0.45f, 1.0f);

            particles[i].position = direction * radius;
            particles[i].startSize = RandomStarSize(random);
            particles[i].startColor = new Color(
                brightness,
                brightness,
                RandomRange(random, brightness * 0.92f, 1.0f),
                1.0f
            );
            particles[i].startLifetime = 999999.0f;
            particles[i].remainingLifetime = 999999.0f;
        }

        starParticles.SetParticles(particles, particles.Length);
        starParticles.Play();
    }

    Material CreateStarMaterial()
    {
        return RuntimeMaterialFactory.CreateParticleMaterial(Color.white);
    }

    Vector3 RandomDirection(System.Random random)
    {
        double z = RandomRange(random, -1.0, 1.0);
        double angle = RandomRange(random, 0.0, System.Math.PI * 2.0);
        double radius = System.Math.Sqrt(1.0 - z * z);

        return new Vector3(
            (float)(radius * System.Math.Cos(angle)),
            (float)(radius * System.Math.Sin(angle)),
            (float)z
        );
    }

    float RandomRange(System.Random random, float min, float max)
    {
        return (float)RandomRange(random, (double)min, max);
    }

    float RandomStarSize(System.Random random)
    {
        double sizeRoll = random.NextDouble();

        if (sizeRoll < 0.82)
        {
            return RandomRange(random, minStarSize, minStarSize * 2.2f);
        }

        if (sizeRoll < 0.97)
        {
            return RandomRange(random, minStarSize * 2.2f, maxStarSize * 0.55f);
        }

        return RandomRange(random, maxStarSize * 0.55f, maxStarSize);
    }

    double RandomRange(System.Random random, double min, double max)
    {
        return min + (max - min) * random.NextDouble();
    }
}
