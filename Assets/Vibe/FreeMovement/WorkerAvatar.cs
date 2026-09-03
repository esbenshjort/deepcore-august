using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// F0.5: physical person presence in the world.
    /// Transform is always "where is this person?" — visibility is separate.
    /// No job AI, pathfinding, or social systems.
    /// </summary>
    public sealed class WorkerAvatar : MonoBehaviour
    {
        SpriteRenderer _body;
        SpriteRenderer _marker;
        Transform _visualRoot;
        bool _devForceShowHidden;

        public int WorkerId { get; private set; }
        public string DisplayName { get; private set; } = "";
        /// <summary>True when hidden for assignment (unless DEV force-show).</summary>
        public bool IsVisuallyHidden { get; private set; }
        /// <summary>Provider id currently syncing presence, or empty when independent.</summary>
        public string FollowingProviderId { get; private set; } = "";
        public Vector2 PresencePosition => transform.localPosition;

        public static WorkerAvatar Spawn(
            Transform parent,
            int workerId,
            string displayName,
            Vector2 localPos)
        {
            var go = new GameObject($"Avatar_{displayName}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var av = go.AddComponent<WorkerAvatar>();
            av.WorkerId = workerId;
            av.DisplayName = displayName ?? $"Worker {workerId}";
            av.BuildVisual();
            av.Show();
            return av;
        }

        void BuildVisual()
        {
            _visualRoot = new GameObject("Visual").transform;
            _visualRoot.SetParent(transform, false);

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(_visualRoot, false);
            _body = bodyGo.AddComponent<SpriteRenderer>();
            _body.sprite = DigVisualKit.Pixel;
            _body.sortingOrder = 38;
            DigVisualKit.ApplyLit(_body);
            bodyGo.transform.localScale = new Vector3(0.22f, 0.28f, 1f);
            _body.color = BodyColorForId(WorkerId);

            var markGo = new GameObject("Marker");
            markGo.transform.SetParent(_visualRoot, false);
            markGo.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            _marker = markGo.AddComponent<SpriteRenderer>();
            _marker.sprite = DigVisualKit.Pixel;
            _marker.sortingOrder = 39;
            DigVisualKit.ApplyLit(_marker);
            markGo.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
            _marker.color = new Color(0.3f, 0.95f, 1f, 0.9f);

            var light = bodyGo.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(light,
                new Color(0.35f, 0.9f, 1f),
                intensity: 0.06f,
                outer: 0.18f,
                inner: 0.02f,
                shadows: false,
                falloff: 0.9f);
        }

        static Color BodyColorForId(int id) => id switch
        {
            1 => new Color(0.35f, 0.85f, 1f, 1f),
            2 => new Color(1f, 0.7f, 0.25f, 1f),
            3 => new Color(0.4f, 0.95f, 0.45f, 1f),
            4 => new Color(0.75f, 0.55f, 1f, 1f),
            5 => new Color(1f, 0.55f, 0.3f, 1f),
            _ => new Color(0.7f, 0.75f, 0.8f, 1f),
        };

        public void Show()
        {
            IsVisuallyHidden = false;
            ApplyVisualActive(true);
        }

        public void Hide()
        {
            IsVisuallyHidden = true;
            ApplyVisualActive(_devForceShowHidden);
        }

        /// <summary>DEV: show ghost of hidden avatars to verify sync.</summary>
        public void SetDevForceShowHidden(bool on)
        {
            _devForceShowHidden = on;
            if (IsVisuallyHidden)
                ApplyVisualActive(on);
            if (on && _body != null && IsVisuallyHidden)
                _body.color = new Color(_body.color.r, _body.color.g, _body.color.b, 0.35f);
            else if (_body != null && !IsVisuallyHidden)
                _body.color = BodyColorForId(WorkerId);
        }

        void ApplyVisualActive(bool on)
        {
            if (_visualRoot != null)
                _visualRoot.gameObject.SetActive(on);
        }

        /// <summary>Set world-local presence. Always updates Transform (even when hidden).</summary>
        public void SetPresencePosition(Vector2 localPos)
        {
            transform.localPosition = localPos;
        }

        public void ParkAt(Vector2 localPos) => SetPresencePosition(localPos);

        public void SetFollowing(string providerId)
        {
            FollowingProviderId = providerId ?? "";
        }

        public void ClearFollowing()
        {
            FollowingProviderId = "";
        }
    }
}
