using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// F0.5: physical person presence in the world.
    /// Transform is always "where is this person?" — visibility is separate.
    /// No job AI, pathfinding, or social systems.
    /// Visual Polish V1: light walk bob, injury/exhaust tint, stumble dust — driven by live WorkerRuntime.
    /// </summary>
    public sealed class WorkerAvatar : MonoBehaviour
    {
        SpriteRenderer _body;
        SpriteRenderer _idBead;
        Transform _visualRoot;
        bool _devForceShowHidden;
        Color _baseTint = Color.white;
        Color _beadBase = Color.white;
        WorkerRuntime _wr;
        Vector2 _lastPos;
        float _bobPhase;
        float _stumbleTilt;
        WorkerFootingEvent _seenFooting;

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
            av._lastPos = localPos;
            av.Show();
            return av;
        }

        public void BindWorker(WorkerRuntime wr) => _wr = wr;

        void BuildVisual()
        {
            _visualRoot = new GameObject("Visual").transform;
            _visualRoot.SetParent(transform, false);

            _body = CrewVisualKit.AttachOffDuty(_visualRoot, WorkerId);
            _baseTint = Color.white;
            if (_body != null)
                _body.color = _baseTint;

            // Quiet identity bead above helmet — matches cyber HUD accents, not a toy marker.
            var markGo = new GameObject("IdBead");
            markGo.transform.SetParent(_visualRoot, false);
            markGo.transform.localPosition = new Vector3(0f, 0.34f, 0f);
            markGo.transform.localScale = Vector3.one * 0.045f;
            _idBead = markGo.AddComponent<SpriteRenderer>();
            _idBead.sprite = DigVisualKit.Pixel;
            _idBead.sortingOrder = 45;
            DigVisualKit.ApplyLit(_idBead);
            _beadBase = AccentForId(WorkerId);
            _idBead.color = _beadBase;

            FootstepDustFx.Attach(transform, () => !IsVisuallyHidden || _devForceShowHidden);
        }

        void LateUpdate()
        {
            if (_visualRoot == null || !_visualRoot.gameObject.activeInHierarchy) return;

            Vector2 pos = PresencePosition;
            Vector2 delta = pos - _lastPos;
            float moved = delta.magnitude;
            bool moving = moved > 0.0004f;
            _lastPos = pos;

            // Walk bob — slower / flatter when injured or exhausted
            float bobAmp = 0.018f;
            float bobHz = 9.5f;
            if (_wr?.State != null)
            {
                if (_wr.State.ExhaustionLatched) { bobAmp *= 0.55f; bobHz *= 0.72f; }
                if (_wr.State.Incapacitated) { bobAmp = 0f; }
                else if (_wr.Injuries != null && WorkerInjuryConsequences.RestrictsWalking(_wr.Injuries))
                {
                    bobAmp *= 0.65f;
                    bobHz *= 0.8f;
                }
            }
            if (moving && bobAmp > 0.001f)
                _bobPhase += Time.deltaTime * bobHz * Mathf.Clamp(moved / 0.02f, 0.4f, 1.6f);
            float bob = moving ? Mathf.Sin(_bobPhase) * bobAmp : 0f;

            // Stumble kick — one brief tilt + dust when locomotion reports stumble/fall
            if (_wr?.Locomotion != null)
            {
                var ev = _wr.Locomotion.LastEvent;
                if (ev != _seenFooting
                    && (ev == WorkerFootingEvent.Stumble || ev == WorkerFootingEvent.LossOfFooting))
                {
                    _stumbleTilt = ev == WorkerFootingEvent.LossOfFooting ? 14f : 8f;
                    FootstepDustFx.Spawn(transform.parent, pos, delta.sqrMagnitude > 0.0001f
                        ? delta.normalized
                        : Vector2.up);
                    if (ev == WorkerFootingEvent.LossOfFooting)
                        FootstepDustFx.Spawn(transform.parent, pos + Vector2.right * 0.05f, Vector2.left);
                }
                _seenFooting = ev;
            }
            _stumbleTilt = Mathf.MoveTowards(_stumbleTilt, 0f, Time.deltaTime * 28f);

            _visualRoot.localPosition = new Vector3(0f, bob, 0f);
            _visualRoot.localRotation = Quaternion.Euler(0f, 0f, _stumbleTilt * (_bobPhase % 2f > 1f ? 1f : -1f));

            // Body / bead tint from physical state (roster still owns emotional read)
            if (_body != null && !_devForceShowHidden)
            {
                Color tint = _baseTint;
                if (_wr?.State != null)
                {
                    if (!_wr.State.IsAlive)
                        tint = new Color(0.35f, 0.35f, 0.38f, 1f);
                    else if (_wr.State.Incapacitated)
                        tint = new Color(0.95f, 0.55f, 0.5f, 1f);
                    else if (_wr.State.NeedsCare || (_wr.State.Injury >= 40f))
                        tint = Color.Lerp(_baseTint, new Color(1f, 0.72f, 0.68f, 1f), 0.45f);
                    else if (_wr.State.ExhaustionLatched)
                        tint = Color.Lerp(_baseTint, new Color(0.85f, 0.78f, 0.65f, 1f), 0.35f);
                }
                if (!IsVisuallyHidden)
                    _body.color = Color.Lerp(_body.color, tint, Time.deltaTime * 6f);
            }
            if (_idBead != null && !IsVisuallyHidden)
            {
                Color bead = _beadBase;
                if (_wr?.State != null && (_wr.State.NeedsCare || _wr.State.Incapacitated))
                    bead = Color.Lerp(_beadBase, new Color(1f, 0.35f, 0.25f, 0.95f), 0.65f);
                else if (_wr?.State != null && _wr.State.ExhaustionLatched)
                    bead = Color.Lerp(_beadBase, new Color(1f, 0.7f, 0.25f, 0.9f), 0.5f);
                _idBead.color = bead;
            }
        }

        static Color AccentForId(int id) => id switch
        {
            1 => new Color(0.35f, 0.9f, 1f, 0.85f),
            2 => new Color(1f, 0.7f, 0.25f, 0.85f),
            3 => new Color(0.4f, 0.95f, 0.45f, 0.85f),
            4 => new Color(0.75f, 0.55f, 1f, 0.85f),
            5 => new Color(1f, 0.55f, 0.3f, 0.85f),
            _ => HiredAccent(id),
        };

        static Color HiredAccent(int id)
        {
            int h = id * 397 ^ (id << 3);
            if (h < 0) h = -h;
            float hue = (h % 360) / 360f;
            var c = Color.HSVToRGB(hue, 0.55f, 0.95f);
            c.a = 0.85f;
            return c;
        }

        public void Show()
        {
            IsVisuallyHidden = false;
            ApplyVisualActive(true);
            if (_body != null)
                _body.color = _baseTint;
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
                _body.color = new Color(_baseTint.r, _baseTint.g, _baseTint.b, 0.35f);
            else if (_body != null && !IsVisuallyHidden)
                _body.color = _baseTint;
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
