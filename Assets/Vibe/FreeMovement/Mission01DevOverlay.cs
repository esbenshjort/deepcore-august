using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>DEV-only Mission 01 route / hazard overlay.</summary>
    public sealed class Mission01DevOverlay : MonoBehaviour
    {
        FineTerrainWorld _world;
        readonly List<LineRenderer> _lines = new();
        bool _visible;

        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                for (int i = 0; i < _lines.Count; i++)
                    if (_lines[i] != null) _lines[i].enabled = value;
            }
        }

        public static Mission01DevOverlay Attach(Transform parent, FineTerrainWorld world)
        {
            var go = new GameObject("Mission01DevOverlay");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<Mission01DevOverlay>();
            fx._world = world;
            return fx;
        }

        public void Rebuild(Mission01Geology.ValidationResult validation)
        {
            ClearLines();
            if (_world == null || validation == null) return;

            AddPath(validation.GoldPathA, new Color(0.2f, 1f, 0.45f, 0.85f), "GoldRouteA");
            AddPath(validation.GoldPathB, new Color(0.55f, 1f, 0.7f, 0.7f), "GoldRouteB");
            AddPath(validation.DiamondPathA, new Color(0.35f, 0.85f, 1f, 0.85f), "DiaRouteA");
            AddPath(validation.DiamondPathB, new Color(0.6f, 0.95f, 1f, 0.7f), "DiaRouteB");

            MarkPoint(validation.Layout.GoldCenter, new Color(1f, 0.85f, 0.2f, 1f), "GoldTarget");
            MarkPoint(validation.Layout.DiamondCenter, new Color(0.5f, 0.9f, 1f, 1f), "DiaTarget");
            MarkPoint(validation.Layout.DirectBedrockPlug, new Color(1f, 0.35f, 0.25f, 1f), "BedrockPlug");
            MarkPoint(validation.Layout.GoldTemptGas, new Color(0.75f, 0.35f, 1f, 1f), "GoldGas");
            MarkPoint(validation.Layout.DiamondTemptGas, new Color(0.75f, 0.35f, 1f, 1f), "DiaGas");
            Visible = _visible;
        }

        void AddPath(List<Vector2Int> path, Color col, string name)
        {
            if (path == null || path.Count < 2) return;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = path.Count;
            lr.startWidth = 0.08f;
            lr.endWidth = 0.05f;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.useWorldSpace = true;
            lr.sortingOrder = 60;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) lr.material = new Material(sh) { color = col };
            lr.startColor = col;
            lr.endColor = col;
            for (int i = 0; i < path.Count; i++)
            {
                var c = _world.CellCenter(path[i].x, path[i].y);
                lr.SetPosition(i, new Vector3(c.x, c.y, -0.2f));
            }
            lr.enabled = _visible;
            _lines.Add(lr);
        }

        void MarkPoint(Vector2Int cell, Color col, string name)
        {
            if (!_world.InBounds(cell.x, cell.y)) return;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 5;
            lr.startWidth = 0.1f;
            lr.endWidth = 0.1f;
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.sortingOrder = 61;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) lr.material = new Material(sh) { color = col };
            lr.startColor = col;
            lr.endColor = col;
            var c = _world.CellCenter(cell.x, cell.y);
            float r = 0.35f;
            lr.SetPosition(0, new Vector3(c.x - r, c.y - r, -0.2f));
            lr.SetPosition(1, new Vector3(c.x + r, c.y - r, -0.2f));
            lr.SetPosition(2, new Vector3(c.x + r, c.y + r, -0.2f));
            lr.SetPosition(3, new Vector3(c.x - r, c.y + r, -0.2f));
            lr.SetPosition(4, new Vector3(c.x - r, c.y - r, -0.2f));
            lr.enabled = _visible;
            _lines.Add(lr);
        }

        void ClearLines()
        {
            for (int i = 0; i < _lines.Count; i++)
                if (_lines[i] != null) Destroy(_lines[i].gameObject);
            _lines.Clear();
        }

        void OnDestroy() => ClearLines();
    }
}
