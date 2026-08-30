using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Helmet flashlight micro-flicker — warm short cone + soft dust wedge.
    /// </summary>
    public sealed class HelmetFlashlight : MonoBehaviour
    {
        Light2D _spot;
        SpriteRenderer _beam;
        SpriteRenderer _bead;
        float _baseIntensity;
        float _baseOuter;
        float _baseBeamAlpha;
        float _phase;
        float _flickPhase;

        public void Init(Light2D spot, SpriteRenderer beam, SpriteRenderer bead, float baseIntensity)
        {
            _spot = spot;
            _beam = beam;
            _bead = bead;
            _baseIntensity = baseIntensity;
            _baseOuter = spot != null ? spot.pointLightOuterRadius : 0.7f;
            _baseBeamAlpha = beam != null ? beam.color.a : 0.1f;
            _phase = Random.Range(0f, Mathf.PI * 2f);
            _flickPhase = Random.Range(0f, 100f);
        }

        void LateUpdate()
        {
            if (_spot == null) return;

            float dt = Time.deltaTime;
            _phase += dt * 1.1f;
            _flickPhase += dt * 33f;

            float steady = 1f
                + Mathf.Sin(_phase * 0.7f) * 0.012f
                + Mathf.Sin(_phase * 1.8f + 0.3f) * 0.008f;
            float micro = 1f
                + Mathf.Sin(_flickPhase) * 0.016f
                + Mathf.Sin(_flickPhase * 2.2f + 1f) * 0.009f;
            float dip = 1f;
            float n = Mathf.PerlinNoise(_flickPhase * 0.1f, _phase * 0.06f);
            if (n > 0.9f)
                dip = 0.92f + 0.08f * Mathf.Sin(_flickPhase * 7f);

            float mul = steady * micro * dip;
            _spot.intensity = _baseIntensity * mul;
            _spot.pointLightOuterRadius = _baseOuter * (0.988f + 0.018f * Mathf.Sin(_phase * 0.5f));

            if (_beam != null)
            {
                var c = _beam.color;
                c.a = _baseBeamAlpha * (0.9f + 0.1f * mul);
                _beam.color = c;
            }

            if (_bead != null)
            {
                var c = _bead.color;
                c.a = 0.82f + 0.16f * mul;
                _bead.color = c;
            }
        }
    }
}
