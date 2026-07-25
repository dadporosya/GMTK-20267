using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the illusion of an endless road. The taxi (and camera) never move — instead a pool of
/// road chunks scrolls toward -Z at a constant speed. When a chunk passes behind the taxi it is
/// recycled to the front of the line and its terrain is regenerated, so the road, hills and
/// fences appear to flow forever with no end and no rebuild hitches.
///
/// The chunk prefab is authored entirely by you: road, left/right terrain (each with a
/// <see cref="TerrainMeshGenerator"/>), fences, props — whatever you want. This manager only
/// duplicates, scrolls and recycles it; it never assumes a specific layout beyond finding the
/// TerrainMeshGenerators somewhere in the prefab's children.
///
/// Convention here matches the project: world is 3D on the XZ plane, forward is +Z. The road
/// scrolls to -Z so the taxi reads as driving forward.
/// </summary>
public class EndlessRoadManager : MonoBehaviour
{
    [Header("Chunk prefab")]
    [Tooltip("The road chunk prefab. Should contain the road, both terrains and any fences/props.")]
    public GameObject chunkPrefab;

    [Tooltip("Length of one chunk along Z, in world units. MUST match TerrainMeshGenerator.length.")]
    public float chunkLength = 30f;

    [Tooltip("How many chunks live at once. Enough to cover the visible road ahead + a small buffer.")]
    [Min(2)] public int chunkCount = 6;

    [Header("Movement")]
    [Tooltip("Constant scroll speed (units/second). No acceleration — steady cruise.")]
    public float speed = 12f;

    [Tooltip("Global movement direction the world scrolls in. Default -Z = taxi drives forward.")]
    public Vector3 scrollDirection = new Vector3(0f, 0f, -1f);

    [Header("Placement")]
    [Tooltip("Z where the very first (rearmost) chunk starts. Should sit behind the taxi/camera.")]
    public float startZ = -30f;

    [Tooltip("Once a chunk's back edge passes this Z (behind the taxi), it recycles to the front.")]
    public float recycleBehindZ = -60f;

    [Tooltip("Parent for spawned chunks. Defaults to this transform if left empty.")]
    public Transform chunkParent;

    // One live chunk: its transform plus the terrain generators it owns.
    private class Chunk
    {
        public Transform transform;
        public TerrainMeshGenerator[] terrains;
        public float noiseZ; // world-space Z offset used to keep hills continuous between chunks
    }

    private readonly List<Chunk> _chunks = new List<Chunk>();
    private Vector3 _dir;

    private void Awake()
    {
        if (chunkParent == null) chunkParent = transform;
        _dir = scrollDirection.sqrMagnitude > 0.0001f ? scrollDirection.normalized : Vector3.back;
    }

    private void Start()
    {
        if (chunkPrefab == null)
        {
            h.Out("EndlessRoadManager: no chunkPrefab assigned — nothing to spawn.");
            enabled = false;
            return;
        }

        BuildInitialChunks();
    }

    private void Update()
    {
        if (_chunks.Count == 0) return;

        // Constant, frame-rate-independent scroll. deltaTime keeps it smooth without rubber-banding.
        Vector3 step = _dir * (speed * Time.deltaTime);

        float frontZ = FrontMostZ();

        for (int i = 0; i < _chunks.Count; i++)
        {
            Chunk c = _chunks[i];
            c.transform.position += step;

            // Recycle when the chunk has fully passed behind the taxi.
            if (c.transform.position.z <= recycleBehindZ)
            {
                frontZ += chunkLength;
                RecycleToFront(c, frontZ);
            }
        }
    }

    private void BuildInitialChunks()
    {
        float z = startZ;
        float noiseZ = 0f;

        for (int i = 0; i < chunkCount; i++)
        {
            GameObject go = Instantiate(chunkPrefab, chunkParent);
            go.name = $"{chunkPrefab.name}_{i}";
            go.transform.position = new Vector3(0f, 0f, z);

            Chunk c = new Chunk
            {
                transform = go.transform,
                terrains = go.GetComponentsInChildren<TerrainMeshGenerator>(true),
                noiseZ = noiseZ
            };

            RegenerateTerrains(c);
            _chunks.Add(c);

            z += chunkLength;
            noiseZ += chunkLength;
        }
    }

    /// <summary>Move a recycled chunk to a new Z at the front and regenerate its hills seamlessly.</summary>
    private void RecycleToFront(Chunk c, float newZ)
    {
        // Continue the noise from the chunk that is currently front-most so hills line up.
        c.noiseZ = HighestNoiseZ() + chunkLength;

        Vector3 pos = c.transform.position;
        pos.x = 0f;
        pos.y = 0f;
        pos.z = newZ;
        c.transform.position = pos;

        RegenerateTerrains(c);
    }

    private void RegenerateTerrains(Chunk c)
    {
        if (c.terrains == null) return;
        for (int i = 0; i < c.terrains.Length; i++)
        {
            if (c.terrains[i] != null)
                c.terrains[i].Generate(c.noiseZ);
        }
    }

    private float FrontMostZ()
    {
        float max = float.NegativeInfinity;
        for (int i = 0; i < _chunks.Count; i++)
        {
            float z = _chunks[i].transform.position.z;
            if (z > max) max = z;
        }
        return max;
    }

    private float HighestNoiseZ()
    {
        float max = float.NegativeInfinity;
        for (int i = 0; i < _chunks.Count; i++)
        {
            if (_chunks[i].noiseZ > max) max = _chunks[i].noiseZ;
        }
        return max;
    }

    // Visualise the spawn/recycle band in the editor for quick tuning.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 s = new Vector3(0f, 0f, startZ);
        Gizmos.DrawWireCube(s, new Vector3(6f, 1f, 0.2f));

        Gizmos.color = Color.red;
        Vector3 r = new Vector3(0f, 0f, recycleBehindZ);
        Gizmos.DrawWireCube(r, new Vector3(6f, 1f, 0.2f));
    }
}
