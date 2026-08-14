using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeepCore.Vibe
{
    /// <summary>
    /// Rock view: marching-squares FILL (diagonal edges, no stair-step cell quads)
    /// plus a detailed organic lip. Square grid remains data-only.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CaveWallMeshRenderer : MonoBehaviour
    {
        [SerializeField] Color rockFill = new(0.05f, 0.055f, 0.06f, 1f);
        [SerializeField] Color rockDeep = new(0.02f, 0.022f, 0.025f, 1f);
        [SerializeField] Color rockLip = new(0.22f, 0.18f, 0.14f, 1f);
        [SerializeField] Color rockLipHi = new(0.4f, 0.32f, 0.22f, 1f);
        [SerializeField] float lipWidth = 0.32f;
        [SerializeField] int smoothIterations = 3;

        CaveGrid _grid;
        Mesh _mesh;

        readonly List<Vector3> _verts = new();
        readonly List<Color> _colors = new();
        readonly List<int> _tris = new();

        public void Bind(CaveGrid grid)
        {
            _grid = grid;
            var filter = GetComponent<MeshFilter>();
            var renderer = GetComponent<MeshRenderer>();

            _mesh = new Mesh { name = "CaveWallMesh" };
            _mesh.MarkDynamic();
            filter.sharedMesh = _mesh;

            renderer.sharedMaterial = CreateUnlitVertexColorMaterial();
            renderer.sortingOrder = 10;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _grid.Changed -= Rebuild;
            _grid.Changed += Rebuild;
            Rebuild();
        }

        void OnDestroy()
        {
            if (_grid != null) _grid.Changed -= Rebuild;
            if (_mesh != null) Destroy(_mesh);
        }

        public void Rebuild()
        {
            if (_grid == null) return;
            _verts.Clear();
            _colors.Clear();
            _tris.Clear();

            BuildMarchingSquaresFill();
            BuildDetailedEdgeLips();

            _mesh.Clear();
            if (_verts.Count == 0) return;
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
        }

        void BuildMarchingSquaresFill()
        {
            for (int y = 0; y < _grid.Height; y++)
            for (int x = 0; x < _grid.Width; x++)
            {
                bool sw = CornerSolid(x, y);
                bool se = CornerSolid(x + 1, y);
                bool ne = CornerSolid(x + 1, y + 1);
                bool nw = CornerSolid(x, y + 1);
                int idx = (sw ? 1 : 0) | (se ? 2 : 0) | (ne ? 4 : 0) | (nw ? 8 : 0);
                if (idx == 0) continue;

                Vector2 pSw = _grid.CellCornerWorld(x, y);
                Vector2 pSe = _grid.CellCornerWorld(x + 1, y);
                Vector2 pNe = _grid.CellCornerWorld(x + 1, y + 1);
                Vector2 pNw = _grid.CellCornerWorld(x, y + 1);
                Vector2 pS = Vector2.Lerp(pSw, pSe, 0.5f);
                Vector2 pE = Vector2.Lerp(pSe, pNe, 0.5f);
                Vector2 pN = Vector2.Lerp(pNw, pNe, 0.5f);
                Vector2 pW = Vector2.Lerp(pSw, pNw, 0.5f);

                Color fill = idx == 15 ? rockDeep : rockFill;

                switch (idx)
                {
                    case 1: Tri(pSw, pS, pW, fill); break;
                    case 2: Tri(pSe, pE, pS, fill); break;
                    case 3: Quad(pSw, pSe, pE, pW, fill); break;
                    case 4: Tri(pNe, pN, pE, fill); break;
                    case 5:
                        Tri(pSw, pS, pW, fill);
                        Tri(pNe, pN, pE, fill);
                        break;
                    case 6: Quad(pSe, pE, pN, pS, fill); break;
                    case 7:
                        Tri(pSw, pSe, pE, fill);
                        Tri(pSw, pE, pN, fill);
                        Tri(pSw, pN, pW, fill);
                        break;
                    case 8: Tri(pNw, pW, pN, fill); break;
                    case 9: Quad(pSw, pS, pN, pNw, fill); break;
                    case 10:
                        Tri(pSe, pE, pS, fill);
                        Tri(pNw, pW, pN, fill);
                        break;
                    case 11:
                        Tri(pSw, pSe, pE, fill);
                        Tri(pSw, pE, pN, fill);
                        Tri(pSw, pN, pNw, fill);
                        break;
                    case 12: Quad(pNw, pW, pE, pNe, fill); break;
                    case 13:
                        Tri(pSw, pS, pE, fill);
                        Tri(pSw, pE, pNe, fill);
                        Tri(pSw, pNe, pNw, fill);
                        break;
                    case 14:
                        Tri(pSe, pE, pNe, fill);
                        Tri(pSe, pNe, pNw, fill);
                        Tri(pSe, pNw, pS, fill);
                        break;
                    case 15: Quad(pSw, pSe, pNe, pNw, fill); break;
                }
            }
        }

        void BuildDetailedEdgeLips()
        {
            var segments = new List<(Vector2 a, Vector2 b)>();

            for (int y = 0; y < _grid.Height; y++)
            for (int x = 0; x < _grid.Width; x++)
            {
                bool sw = CornerSolid(x, y);
                bool se = CornerSolid(x + 1, y);
                bool ne = CornerSolid(x + 1, y + 1);
                bool nw = CornerSolid(x, y + 1);
                int idx = (sw ? 1 : 0) | (se ? 2 : 0) | (ne ? 4 : 0) | (nw ? 8 : 0);
                if (idx == 0 || idx == 15) continue;

                Vector2 pSw = _grid.CellCornerWorld(x, y);
                Vector2 pSe = _grid.CellCornerWorld(x + 1, y);
                Vector2 pNe = _grid.CellCornerWorld(x + 1, y + 1);
                Vector2 pNw = _grid.CellCornerWorld(x, y + 1);
                Vector2 pS = Vector2.Lerp(pSw, pSe, 0.5f);
                Vector2 pE = Vector2.Lerp(pSe, pNe, 0.5f);
                Vector2 pN = Vector2.Lerp(pNw, pNe, 0.5f);
                Vector2 pW = Vector2.Lerp(pSw, pNw, 0.5f);

                void Seg(Vector2 a, Vector2 b) => segments.Add((a, b));
                switch (idx)
                {
                    case 1: case 14: Seg(pW, pS); break;
                    case 2: case 13: Seg(pS, pE); break;
                    case 3: case 12: Seg(pW, pE); break;
                    case 4: case 11: Seg(pE, pN); break;
                    case 5: Seg(pW, pN); Seg(pS, pE); break;
                    case 6: case 9: Seg(pS, pN); break;
                    case 7: case 8: Seg(pW, pN); break;
                    case 10: Seg(pW, pS); Seg(pE, pN); break;
                }
            }

            var smooth = new List<Vector2>();
            foreach (var loop in StitchLoops(segments))
            {
                SmoothChaikin(loop, smooth, smoothIterations);
                AddLipRibbon(smooth, lipWidth);
            }
        }

        bool CornerSolid(int cx, int cy) => _grid.IsRockOrEdge(cx - 1, cy - 1);

        void Tri(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int i = _verts.Count;
            _verts.Add(new Vector3(a.x, a.y, 0f));
            _verts.Add(new Vector3(b.x, b.y, 0f));
            _verts.Add(new Vector3(c.x, c.y, 0f));
            _colors.Add(color); _colors.Add(color); _colors.Add(color);
            _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
        }

        void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            Tri(a, b, c, color);
            Tri(a, c, d, color);
        }

        List<List<Vector2>> StitchLoops(List<(Vector2 a, Vector2 b)> segments)
        {
            var loops = new List<List<Vector2>>();
            var unused = new List<(Vector2 a, Vector2 b)>(segments);
            const float eps = 0.001f;

            while (unused.Count > 0)
            {
                var loop = new List<Vector2>();
                var (a0, b0) = unused[^1];
                unused.RemoveAt(unused.Count - 1);
                loop.Add(a0);
                loop.Add(b0);

                bool grew = true;
                while (grew)
                {
                    grew = false;
                    Vector2 head = loop[0];
                    Vector2 tail = loop[^1];
                    for (int i = unused.Count - 1; i >= 0; i--)
                    {
                        var (a, b) = unused[i];
                        if (Near(tail, a, eps)) { loop.Add(b); unused.RemoveAt(i); grew = true; break; }
                        if (Near(tail, b, eps)) { loop.Add(a); unused.RemoveAt(i); grew = true; break; }
                        if (Near(head, a, eps)) { loop.Insert(0, b); unused.RemoveAt(i); grew = true; break; }
                        if (Near(head, b, eps)) { loop.Insert(0, a); unused.RemoveAt(i); grew = true; break; }
                    }
                }
                if (loop.Count >= 3) loops.Add(loop);
            }
            return loops;
        }

        static bool Near(Vector2 a, Vector2 b, float eps) => (a - b).sqrMagnitude <= eps * eps;

        static void SmoothChaikin(List<Vector2> src, List<Vector2> dst, int iterations)
        {
            dst.Clear();
            dst.AddRange(src);
            for (int it = 0; it < iterations; it++)
            {
                if (dst.Count < 3) return;
                var next = new List<Vector2>(dst.Count * 2);
                bool closed = Near(dst[0], dst[^1], 0.05f);
                int n = closed ? dst.Count - 1 : dst.Count;
                if (!closed) next.Add(dst[0]);
                for (int i = 0; i < n; i++)
                {
                    Vector2 p0 = dst[i];
                    Vector2 p1 = dst[(i + 1) % dst.Count];
                    next.Add(Vector2.Lerp(p0, p1, 0.25f));
                    next.Add(Vector2.Lerp(p0, p1, 0.75f));
                }
                if (!closed) next.Add(dst[^1]);
                else next.Add(next[0]);
                dst.Clear();
                dst.AddRange(next);
            }
        }

        void AddLipRibbon(List<Vector2> loop, float width)
        {
            int n = loop.Count;
            bool closed = Near(loop[0], loop[n - 1], 0.05f);
            int count = closed ? n - 1 : n;
            if (count < 2) return;

            for (int i = 0; i < count - (closed ? 0 : 1); i++)
            {
                Vector2 p0 = loop[i];
                Vector2 p1 = loop[(i + 1) % n];
                Vector2 dir = p1 - p0;
                if (dir.sqrMagnitude < 1e-8f) continue;
                dir.Normalize();
                Vector2 nrm = new(-dir.y, dir.x);

                Vector2 mid = (p0 + p1) * 0.5f;
                if (!PointSamplesRock(mid + nrm * 0.2f) && PointSamplesRock(mid - nrm * 0.2f))
                    nrm = -nrm;

                float wob = 1f + 0.4f * Mathf.Sin(mid.x * 6.2f + mid.y * 4.8f);
                float w = width * wob;

                Vector2 a0 = p0;
                Vector2 a1 = p1;
                Vector2 b0 = p0 + nrm * w;
                Vector2 b1 = p1 + nrm * w;
                Tri(a0, a1, b1, rockLipHi);
                Tri(a0, b1, b0, rockLip);

                Vector2 c0 = p0 - nrm * w * 0.4f;
                Vector2 c1 = p1 - nrm * w * 0.4f;
                Tri(c0, a1, a0, rockFill);
                Tri(c0, c1, a1, rockFill);
            }
        }

        bool PointSamplesRock(Vector2 world)
        {
            var cell = _grid.WorldToCell(world);
            return _grid.IsRockOrEdge(cell.x, cell.y);
        }

        static Material CreateUnlitVertexColorMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            return new Material(shader) { name = "CaveWall_VC" };
        }
    }
}
