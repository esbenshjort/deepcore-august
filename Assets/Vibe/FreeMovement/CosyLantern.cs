using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>Warm industrial lantern — gentle sway + amber flicker.</summary>
    public sealed class CosyLantern : MonoBehaviour
    {
        [SerializeField] float swayAmp = 0.035f;
        [SerializeField] float swaySpeed = 1.05f;
        [SerializeField] float flicker = 0.11f;

        Vector3 _base;
        Light2D _light;
        float _phase;
        float _baseIntensity;
        float _baseOuter;

        public void Init(Vector3 pos, Light2D light, float intensity)
        {
            _base = pos;
            transform.position = pos;
            _light = light;
            _baseIntensity = intensity;
            _baseOuter = light != null ? light.pointLightOuterRadius : 3f;
            _phase = Random.Range(0f, Mathf.PI * 2f);
            swaySpeed *= Random.Range(0.9f, 1.12f);
            swayAmp *= Random.Range(0.85f, 1.15f);
        }

        void Update()
        {
            _phase += Time.deltaTime * swaySpeed;
            float sx = Mathf.Sin(_phase) * swayAmp;
            float sy = Mathf.Cos(_phase * 0.7f + 0.4f) * swayAmp * 0.4f;
            transform.position = _base + new Vector3(sx, sy, 0f);
            // Heavy iron — tiny tilt only
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(_phase * 0.9f) * 3.5f);

            if (_light != null)
            {
                float f = 1f + Mathf.Sin(_phase * 3.1f) * flicker
                            + Mathf.Sin(_phase * 5.7f + 1.2f) * flicker * 0.55f
                            + Mathf.Sin(_phase * 11f) * flicker * 0.12f;
                _light.intensity = _baseIntensity * f;
                _light.pointLightOuterRadius = _baseOuter * (0.95f + 0.06f * Mathf.Sin(_phase * 1.7f + 0.5f));
            }
        }
    }
}
