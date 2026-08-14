using UnityEngine;

namespace DeepCore.Vibe
{
    /// <summary>
    /// Legacy entry — redirects to VisualTopologyTest.
    /// </summary>
    public sealed class LookTestBootstrap : MonoBehaviour
    {
        public const int UnitFootprint = 3;

        void Awake()
        {
            if (GetComponent<VisualTopologyTest>() == null)
                gameObject.AddComponent<VisualTopologyTest>();
            enabled = false;
        }
    }
}
