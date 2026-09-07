using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Rugged mine-camp toilet — enclosed stall + tank/vent, separated from food/sleep.
    /// Single occupancy; door swings briefly when someone enters/leaves.
    /// </summary>
    public sealed class CampToiletSite : MonoBehaviour
    {
        public Vector2 Door { get; private set; }
        public Vector2 StallCenter { get; private set; }
        public int OccupantWorkerId { get; private set; } = -1;
        public bool IsOccupied => OccupantWorkerId > 0;

        Transform _doorFlap;
        Quaternion _doorClosed;
        Quaternion _doorOpen;
        float _doorT;
        bool _wantOpen;

        public static CampToiletSite Spawn(Transform parent, Vector2 campCenter, Vector2 tentDoor)
        {
            var go = new GameObject("CampToilet");
            go.transform.SetParent(parent, false);
            var site = go.AddComponent<CampToiletSite>();
            site.Build(campCenter, tentDoor);
            return site;
        }

        void Build(Vector2 campCenter, Vector2 tentDoor)
        {
            // South-east of tent, away from fire/kitchen — service path feel
            StallCenter = tentDoor + new Vector2(1.85f, -1.75f);
            Door = StallCenter + new Vector2(-0.42f, -0.05f);
            transform.localPosition = Vector3.zero;

            var pad = Make("ToiletPad", StallCenter + new Vector2(0.05f, -0.15f),
                YardVisualKit.SleepPad, 10, 0.95f);
            DigVisualKit.ApplyLit(pad);
            pad.color = new Color(0.35f, 0.36f, 0.38f, 0.55f);

            var stall = Make("Stall", StallCenter, YardVisualKit.ToiletStall, 18, 0.72f);
            DigVisualKit.ApplyLit(stall);

            var tank = Make("Tank", StallCenter + new Vector2(0.28f, 0.18f),
                YardVisualKit.ToiletTank, 19, 0.38f);
            DigVisualKit.ApplyLit(tank);

            var vent = Make("Vent", StallCenter + new Vector2(0.22f, 0.42f),
                YardVisualKit.ToiletVent, 20, 0.22f);
            DigVisualKit.ApplyLit(vent);

            var pipe = Make("Pipe", StallCenter + new Vector2(0.38f, -0.05f),
                YardVisualKit.PowerCable, 14, 0.32f);
            DigVisualKit.ApplyLit(pipe);
            pipe.transform.localRotation = Quaternion.Euler(0f, 0f, 72f);

            var sign = Make("WCSign", StallCenter + new Vector2(-0.28f, 0.28f),
                YardVisualKit.ToiletSign, 21, 0.18f);
            DigVisualKit.ApplyLit(sign);

            // Door flap — hinge on west jamb
            var doorSr = Make("DoorFlap", Door + new Vector2(0.02f, 0f),
                YardVisualKit.TentFlap, 22, 0.42f);
            DigVisualKit.ApplyLit(doorSr);
            doorSr.color = new Color(0.42f, 0.4f, 0.36f, 1f);
            _doorFlap = doorSr.transform;
            _doorClosed = Quaternion.Euler(0f, 0f, 0f);
            _doorOpen = Quaternion.Euler(0f, 0f, -72f);
            _doorFlap.localRotation = _doorClosed;

            var lamp = Make("ToiletLamp", StallCenter + new Vector2(-0.35f, 0.35f),
                DigVisualKit.Pixel, 22, 0.07f);
            var unlit = Shader.Find("Sprites/Default");
            if (unlit != null) lamp.sharedMaterial = new Material(unlit);
            lamp.color = new Color(0.45f, 0.95f, 1f, 1f);

            var lightGo = new GameObject("ToiletLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = StallCenter + new Vector2(-0.35f, 0.35f);
            var light = lightGo.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(light,
                new Color(0.4f, 0.9f, 1f),
                intensity: 0.35f,
                outer: 0.55f,
                inner: 0.02f,
                shadows: false,
                falloff: 0.8f);
        }

        public bool TryOccupy(int workerId)
        {
            if (workerId <= 0) return false;
            if (IsOccupied && OccupantWorkerId != workerId) return false;
            OccupantWorkerId = workerId;
            _wantOpen = true;
            return true;
        }

        public void Release(int workerId)
        {
            if (OccupantWorkerId == workerId || workerId <= 0)
            {
                OccupantWorkerId = -1;
                _wantOpen = true; // swing open briefly on exit
            }
        }

        void Update()
        {
            if (_doorFlap == null) return;
            float target = _wantOpen ? 1f : 0f;
            _doorT = Mathf.MoveTowards(_doorT, target, Time.deltaTime * 4.5f);
            _doorFlap.localRotation = Quaternion.Slerp(_doorClosed, _doorOpen, _doorT);
            // Auto-close after open pulse when vacant
            if (_wantOpen && _doorT >= 0.98f && !IsOccupied)
                _wantOpen = false;
            if (IsOccupied && _doorT >= 0.98f)
                _wantOpen = false; // close while occupied inside
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
