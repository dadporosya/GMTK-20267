using UnityEngine;

/// <summary>
/// Builds a procedural "rolling hills" mesh for ONE side (left or right) of a road chunk.
///
/// The strip runs along the road on local +Z (0..length) and outward from the road edge on
/// local +X (0..width). Hills come from layered Perlin noise; the strip is kept flat right next
/// to the road so the tarmac stays readable, then eases up into the hills further out.
///
/// Terrain continuity across chunks: <see cref="Generate"/> takes a worldZOffset. The noise is
/// sampled at (localZ + worldZOffset), so when <see cref="EndlessRoadManager"/> recycles a chunk
/// to the front and passes the next offset, the hills flow seamlessly from the chunk behind it —
/// no visible seams, no popping.
///
/// Put this component on the two terrain child objects inside the road chunk prefab and set
/// <see cref="side"/> to Left / Right. It requires a MeshFilter (+ MeshRenderer for a material).
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class TerrainMeshGenerator : MonoBehaviour
{
    public enum Side { Left, Right }

    [Header("Layout")]
    [Tooltip("Which side of the road this strip sits on. Right mirrors the mesh along local -X.")]
    public Side side = Side.Left;

    [Tooltip("Width of the strip, perpendicular to the road (local X).")]
    public float width = 40f;

    [Tooltip("Length of the strip along the road (local Z). MUST match EndlessRoadManager.chunkLength.")]
    public float length = 30f;

    [Tooltip("Flat band (in X) next to the road before hills start rising, so the road stays clear.")]
    public float innerFlatWidth = 3f;

    [Tooltip("Distance over which the terrain blends from flat (road edge) up to full hill height.")]
    public float innerFadeWidth = 6f;

    [Header("Resolution")]
    [Tooltip("Grid quads across the width. Higher = smoother hills but more verts.")]
    [Min(1)] public int segmentsX = 16;
    [Tooltip("Grid quads along the length. Keep this even-ish with segmentsX for regular quads.")]
    [Min(1)] public int segmentsZ = 16;

    [Header("Hills (Perlin noise)")]
    [Tooltip("Noise frequency. Small = broad, gentle hills. Large = choppy. Keep small for calm fields.")]
    public float noiseScale = 0.03f;

    [Tooltip("Max hill height in world units.")]
    public float heightAmplitude = 6f;

    [Tooltip("How many noise layers to stack for natural-looking hills.")]
    [Range(1, 4)] public int octaves = 2;

    [Tooltip("How quickly each extra octave loses influence (lower = smoother).")]
    [Range(0f, 1f)] public float persistence = 0.45f;

    [Tooltip("Frequency multiplier between octaves.")]
    public float lacunarity = 2f;

    [Tooltip("Constant seed offset so left/right (and different fields) don't look identical.")]
    public float seedOffset = 0f;

    [Header("Behaviour")]
    [Tooltip("Regenerate once on Start using worldZOffset = 0 (useful when testing without the manager).")]
    public bool generateOnStart = true;

    private MeshFilter _mf;
    private Mesh _mesh;

    // Reused buffers so recycling a chunk doesn't allocate garbage every time.
    private Vector3[] _vertices;
    private Vector2[] _uvs;
    private int[] _triangles;
    private int _builtSegX = -1;
    private int _builtSegZ = -1;

    private void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        _mesh = new Mesh { name = "ProceduralTerrain" };
        _mesh.MarkDynamic();
        _mf.sharedMesh = _mesh;
    }

    private void Start()
    {
        if (generateOnStart)
            Generate(0f);
    }

    /// <summary>
    /// Rebuild the hill mesh. <paramref name="worldZOffset"/> is the world-space Z at which this
    /// chunk's local Z=0 currently sits (in "noise space"), which keeps hills continuous between
    /// chunks. The manager passes previousOffset + chunkLength each time it recycles a chunk.
    /// </summary>
    public void Generate(float worldZOffset)
    {
        if (_mesh == null)
        {
            _mf = GetComponent<MeshFilter>();
            _mesh = new Mesh { name = "ProceduralTerrain" };
            _mesh.MarkDynamic();
            _mf.sharedMesh = _mesh;
        }

        int sx = Mathf.Max(1, segmentsX);
        int sz = Mathf.Max(1, segmentsZ);
        int vertsX = sx + 1;
        int vertsZ = sz + 1;
        int vertCount = vertsX * vertsZ;

        // (Re)allocate buffers only when the resolution changes.
        if (_vertices == null || _vertices.Length != vertCount || _builtSegX != sx || _builtSegZ != sz)
        {
            _vertices = new Vector3[vertCount];
            _uvs = new Vector2[vertCount];
            _triangles = new int[sx * sz * 6];
            _builtSegX = sx;
            _builtSegZ = sz;
            BuildTriangles(sx, sz, vertsX);
        }

        float xSign = side == Side.Right ? -1f : 1f;

        for (int zi = 0; zi < vertsZ; zi++)
        {
            float tz = (float)zi / sz;
            float localZ = tz * length;
            float noiseZ = localZ + worldZOffset;

            for (int xi = 0; xi < vertsX; xi++)
            {
                float tx = (float)xi / sx;
                float localX = tx * width;                  // distance out from the road edge
                float worldLikeX = localX + seedOffset;     // separate left/right noise patterns

                float h = SampleHeight(worldLikeX, noiseZ);

                // Keep a flat band by the road, then ease into the hills.
                float fade = Mathf.InverseLerp(innerFlatWidth, innerFlatWidth + innerFadeWidth, localX);
                fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fade));
                h *= fade;

                int idx = zi * vertsX + xi;
                _vertices[idx] = new Vector3(localX * xSign, h, localZ);
                _uvs[idx] = new Vector2(tx, tz);
            }
        }

        _mesh.Clear();
        _mesh.vertices = _vertices;
        _mesh.uv = _uvs;
        _mesh.triangles = _triangles;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    private float SampleHeight(float x, float z)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float sum = 0f;
        float ampSum = 0f;

        for (int o = 0; o < Mathf.Max(1, octaves); o++)
        {
            // +1000 keeps us away from Perlin's mirrored, low-detail region around the origin.
            float sample = Mathf.PerlinNoise(
                (x * noiseScale * frequency) + 1000f,
                (z * noiseScale * frequency) + 1000f);

            sum += sample * amplitude;
            ampSum += amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        float normalized = ampSum > 0f ? sum / ampSum : 0f; // 0..1
        return normalized * heightAmplitude;
    }

    private void BuildTriangles(int sx, int sz, int vertsX)
    {
        int t = 0;
        // Right side is mirrored on X, which flips the winding; flip triangle order so both
        // sides keep their faces pointing up.
        bool flip = side == Side.Right;

        for (int zi = 0; zi < sz; zi++)
        {
            for (int xi = 0; xi < sx; xi++)
            {
                int bl = zi * vertsX + xi;
                int br = bl + 1;
                int tl = bl + vertsX;
                int tr = tl + 1;

                if (!flip)
                {
                    _triangles[t++] = bl; _triangles[t++] = tl; _triangles[t++] = tr;
                    _triangles[t++] = bl; _triangles[t++] = tr; _triangles[t++] = br;
                }
                else
                {
                    _triangles[t++] = bl; _triangles[t++] = tr; _triangles[t++] = tl;
                    _triangles[t++] = bl; _triangles[t++] = br; _triangles[t++] = tr;
                }
            }
        }
    }
}
