using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Reuses <see cref="Mesh"/> shells evicted from <see cref="PlanetFlightChunkMeshCache"/>.
/// Skips Unity-destroyed instances left in the static stack after Play Mode stop.
/// </summary>
public static class PlanetMeshPool
{
    const int MaxPoolSize = 192;

    static readonly Stack<Mesh> Pool = new Stack<Mesh>(MaxPoolSize);

    public static int Count => Pool.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Pool.Clear();
    }

    public static Mesh Rent(string meshName, int vertexCount)
    {
        while (Pool.Count > 0)
        {
            Mesh mesh = Pool.Pop();
            if (mesh == null)
            {
                continue;
            }

            mesh.Clear(keepVertexLayout: false);
            mesh.name = meshName;
            mesh.indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            return mesh;
        }

        return new Mesh
        {
            name = meshName,
            indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
        };
    }

    public static void Release(Mesh mesh)
    {
        if (mesh == null)
        {
            return;
        }

        if (Pool.Count >= MaxPoolSize)
        {
            Object.Destroy(mesh);
            return;
        }

        Pool.Push(mesh);
    }

    public static void Clear()
    {
        while (Pool.Count > 0)
        {
            Mesh mesh = Pool.Pop();
            if (mesh != null)
            {
                Object.Destroy(mesh);
            }
        }
    }
}
