using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bakes the NavMesh over the building when the scene starts.
/// Uses the built-in AI module, so no extra package is needed.
/// Runs before every other script so the killer always has a NavMesh.
/// </summary>
[DefaultExecutionOrder(-200)]
public class RuntimeNavMeshBaker : MonoBehaviour
{
    [Tooltip("Only colliders under these objects are baked (map + hiding spots).")]
    public Transform[] roots;

    [Tooltip("Size of the baked area, centered on this object.")]
    public Vector3 size = new Vector3(70f, 10f, 50f);

    [Tooltip("Layers that count as walkable / blocking geometry.")]
    public LayerMask includeLayers = 1; // Default layer only (triggers live on Ignore Raycast)

    public static bool IsReady { get; private set; }

    NavMeshDataInstance instance;

    void Awake() { Bake(); }

    public void Bake()
    {
        var sources = new List<NavMeshBuildSource>();
        var markups = new List<NavMeshBuildMarkup>();
        var rootSources = new List<NavMeshBuildSource>();
        foreach (Transform root in roots)
        {
            if (!root) continue;
            // CollectSources clears its result list, so gather per root and merge.
            NavMeshBuilder.CollectSources(root, includeLayers, NavMeshCollectGeometry.PhysicsColliders, 0, markups, rootSources);
            sources.AddRange(rootSources);
        }

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        var bounds = new Bounds(transform.position, size);
        NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        if (data == null)
        {
            Debug.LogError("[NavMesh] Bake failed.");
            return;
        }

        instance = NavMesh.AddNavMeshData(data);
        IsReady = true;
    }

    void OnDestroy()
    {
        if (instance.valid) instance.Remove();
        IsReady = false;
    }
}
