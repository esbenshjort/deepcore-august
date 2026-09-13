using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Camp field-analysis table — Prospector desk + Refiner consult meet.
    /// Visual only for the map surface; does not run a second scan/tactical system.
    /// </summary>
    public sealed class ProspectorWorkstationSite : MonoBehaviour
    {
        public Vector2 TableCenter { get; private set; }
        /// <summary>Where the Prospector stands while analysing.</summary>
        public Vector2 ProspectorStand { get; private set; }
        /// <summary>Where the Refiner stands during consultation.</summary>
        public Vector2 RefinerStand { get; private set; }

        SpriteRenderer _mapSr;
        Light2D _mapGlow;
        bool _activeWork;

        public static ProspectorWorkstationSite Spawn(Transform parent, Vector2 washerPos)
        {
            var go = new GameObject("ProspectorWorkstation");
            go.transform.SetParent(parent, false);
            var site = go.AddComponent<ProspectorWorkstationSite>();
            site.Build(washerPos);
            return site;
        }

        void Build(Vector2 washerPos)
        {
            // East of wash bay — fills the empty right pad beside the platform
            TableCenter = washerPos + new Vector2(1.72f, -0.08f);
            ProspectorStand = TableCenter + new Vector2(-0.42f, -0.32f);
            RefinerStand = TableCenter + new Vector2(0.44f, -0.28f);
            transform.localPosition = Vector3.zero;

            var pad = Make("TablePad", TableCenter + new Vector2(0.02f, -0.12f),
                YardVisualKit.SleepPad, 11, 1.15f);
            DigVisualKit.ApplyLit(pad);
            pad.color = new Color(0.32f, 0.33f, 0.35f, 0.5f);

            var table = Make("AnalysisTable", TableCenter, YardVisualKit.AnalysisTable, 18, 0.92f);
            DigVisualKit.ApplyLit(table);

            _mapSr = Make("MapSurface", TableCenter + new Vector2(0f, 0.06f),
                YardVisualKit.AnalysisMapSurface, 19, 0.48f);
            var unlit = Shader.Find("Sprites/Default");
            if (unlit != null) _mapSr.sharedMaterial = new Material(unlit);
            _mapSr.color = new Color(0.45f, 0.9f, 1f, 0.72f);

            // Small instrument crate / lamp post feel
            var crate = Make("InstCrate", TableCenter + new Vector2(-0.55f, 0.15f),
                YardVisualKit.Crate, 17, 0.32f);
            DigVisualKit.ApplyLit(crate);

            var lamp = Make("TableLamp", TableCenter + new Vector2(0.38f, 0.22f),
                DigVisualKit.Pixel, 21, 0.08f);
            if (unlit != null) lamp.sharedMaterial = new Material(unlit);
            lamp.color = new Color(0.4f, 0.95f, 1f, 1f);

            var lightGo = new GameObject("TableLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = TableCenter + new Vector2(0.05f, 0.12f);
            _mapGlow = lightGo.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(_mapGlow,
                new Color(0.3f, 0.88f, 1f),
                intensity: 0.32f,
                outer: 0.75f,
                inner: 0.04f,
                shadows: false,
                falloff: 0.78f);

            DigVisualKit.PlaceGroundingPad(transform, TableCenter + new Vector2(0.02f, -0.22f), 0.9f,
                new Color(0.22f, 0.24f, 0.26f, 0.42f), sortingOrder: 10);
            DigVisualKit.PlaceUtilityCable(transform,
                TableCenter + new Vector2(-0.35f, -0.35f), 0.28f, 18f,
                new Color(0.28f, 0.32f, 0.35f, 0.8f));
        }

        public void SetWorkActive(bool active)
        {
            _activeWork = active;
            if (_mapSr != null)
                _mapSr.color = active
                    ? new Color(0.55f, 0.95f, 1f, 0.9f)
                    : new Color(0.45f, 0.9f, 1f, 0.72f);
            if (_mapGlow != null)
                _mapGlow.intensity = active ? 0.42f : 0.28f;
        }

        void Update()
        {
            if (!_activeWork || _mapSr == null) return;
            float pulse = 0.82f + 0.18f * Mathf.Sin(Time.time * 3.2f);
            var c = _mapSr.color;
            c.a = pulse;
            _mapSr.color = c;
        }

        SpriteRenderer Make(string name, Vector2 pos, Sprite sprite, int order, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            go.transform.localScale = Vector3.one * scale;
            return sr;
        }
    }
}
