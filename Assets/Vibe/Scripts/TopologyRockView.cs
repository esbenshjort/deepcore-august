using UnityEngine;

namespace DeepCore.Vibe
{
    /// <summary>
    /// Per-cell topology sprites from 8 neighbours. Hard alpha cutouts over floor.
    /// </summary>
    public sealed class TopologyRockView : MonoBehaviour
    {
        CaveGrid _grid;
        Transform _rockRoot;
        Transform _labelRoot;
        GameObject[] _visuals;
        Sprite[] _sprites;
        bool _debugColors;
        bool _showLabels;
        static Material _spriteMat;

        public void Bind(CaveGrid grid, Transform rockRoot, bool showLabels = false)
        {
            TopologySpriteFactory.ClearCache();
            _grid = grid;
            _rockRoot = rockRoot;
            _showLabels = showLabels;
            _sprites = new Sprite[8];
            for (int i = 0; i < 8; i++)
                _sprites[i] = TopologySpriteFactory.Get((RockTopology8.Kind)i);

            if (_spriteMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                         ?? Shader.Find("Sprites/Default");
                _spriteMat = new Material(sh);
            }

            _visuals = new GameObject[grid.Width * grid.Height];
            if (_labelRoot != null) Destroy(_labelRoot.gameObject);
            _labelRoot = new GameObject("TopoLabels").transform;
            _labelRoot.SetParent(rockRoot.parent, false);

            _grid.Changed -= Rebuild;
            _grid.Changed += Rebuild;
            Rebuild();
        }

        public void SetDebugColors(bool on)
        {
            _debugColors = on;
            Rebuild();
        }

        void OnDestroy()
        {
            if (_grid != null) _grid.Changed -= Rebuild;
        }

        public void Rebuild()
        {
            if (_grid == null) return;

            for (int i = 0; i < _visuals.Length; i++)
            {
                if (_visuals[i] == null) continue;
                Destroy(_visuals[i]);
                _visuals[i] = null;
            }

            if (_labelRoot != null)
            {
                for (int c = _labelRoot.childCount - 1; c >= 0; c--)
                    Destroy(_labelRoot.GetChild(c).gameObject);
            }

            // One label per kind (first occurrence) on showcase
            var labeled = new bool[8];

            for (int y = 0; y < _grid.Height; y++)
            for (int x = 0; x < _grid.Width; x++)
            {
                if (!_grid.IsRock(x, y)) continue;

                bool n = _grid.IsRockOrEdge(x, y + 1);
                bool e = _grid.IsRockOrEdge(x + 1, y);
                bool s = _grid.IsRockOrEdge(x, y - 1);
                bool w = _grid.IsRockOrEdge(x - 1, y);
                bool ne = _grid.IsRockOrEdge(x + 1, y + 1);
                bool se = _grid.IsRockOrEdge(x + 1, y - 1);
                bool sw = _grid.IsRockOrEdge(x - 1, y - 1);
                bool nw = _grid.IsRockOrEdge(x - 1, y + 1);

                var topo = RockTopology8.Resolve(n, ne, e, se, s, sw, w, nw);

                var go = new GameObject($"Rock_{topo.Kind}_{x}_{y}");
                go.transform.SetParent(_rockRoot);
                go.transform.position = _grid.CellToWorld(x, y);
                go.transform.rotation = Quaternion.Euler(0f, 0f, topo.ZRotation);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = _spriteMat;
                sr.sprite = _sprites[(int)topo.Kind];
                sr.flipX = topo.FlipX;
                sr.sortingOrder = 10;
                go.transform.localScale = Vector3.one;
                sr.color = _debugColors ? RockTopology8.DebugColors[(int)topo.Kind] : Color.white;

                _visuals[y * _grid.Width + x] = go;

                int ki = (int)topo.Kind;
                if (_showLabels && !labeled[ki] && topo.Kind != RockTopology8.Kind.Full)
                {
                    labeled[ki] = true;
                    SpawnLabel(topo.Kind, go.transform.position + Vector3.up * 0.55f);
                }
            }

            if (_showLabels && !labeled[(int)RockTopology8.Kind.Full])
            {
                // label a deep interior rock
                for (int y = 1; y < _grid.Height - 1 && !labeled[0]; y++)
                for (int x = 1; x < _grid.Width - 1; x++)
                {
                    if (!_grid.IsRock(x, y)) continue;
                    if (_grid.IsRock(x, y + 1) && _grid.IsRock(x, y - 1) &&
                        _grid.IsRock(x + 1, y) && _grid.IsRock(x - 1, y) &&
                        _grid.IsRock(x + 1, y + 1) && _grid.IsRock(x - 1, y - 1))
                    {
                        labeled[0] = true;
                        SpawnLabel(RockTopology8.Kind.Full, _grid.CellToWorld(x, y));
                        break;
                    }
                }
            }
        }

        void SpawnLabel(RockTopology8.Kind kind, Vector3 pos)
        {
            var go = new GameObject($"Label_{kind}");
            go.transform.SetParent(_labelRoot);
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = kind.ToString();
            tm.characterSize = 0.08f;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.85f, 0.45f, 0.95f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 60;
        }
    }
}
