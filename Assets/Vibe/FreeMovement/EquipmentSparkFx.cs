using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Brief cyan/amber sparks for engineer repair / equipment restart.
    /// Reuses digger spark motion language — not a second particle stack.
    /// </summary>
    public static class EquipmentSparkFx
    {
        public static void SpawnRepair(Transform parent, Vector2 worldPos, int count = 3)
        {
            if (parent == null) return;
            for (int i = 0; i < count; i++)
                SpawnOne(parent, worldPos, repair: true);
        }

        public static void SpawnRestart(Transform parent, Vector2 worldPos)
        {
            if (parent == null) return;
            for (int i = 0; i < 5; i++)
                SpawnOne(parent, worldPos, repair: false);
        }

        static void SpawnOne(Transform parent, Vector2 worldPos, bool repair)
        {
            var go = new GameObject(repair ? "RepairSpark" : "RestartSpark");
            go.transform.SetParent(parent, false);
            go.transform.position = (Vector3)worldPos
                + (Vector3)(Random.insideUnitCircle * 0.14f);
            go.transform.localScale = Vector3.one * Random.Range(0.05f, 0.1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = DigVisualKit.Pixel;
            sr.sortingOrder = 42;
            DigVisualKit.ApplyUnlit(sr);
            bool cyan = Random.value > (repair ? 0.35f : 0.55f);
            sr.color = cyan
                ? new Color(0.45f, 0.95f, 1f, Random.Range(0.8f, 1f))
                : new Color(1f, 0.72f, 0.25f, Random.Range(0.75f, 1f));

            Vector2 kick = Random.insideUnitCircle.normalized * Random.Range(0.4f, 1.05f)
                           + Vector2.up * Random.Range(0.15f, 0.45f);
            go.AddComponent<DrillSparkPuff>().Init(
                kick,
                life: Random.Range(0.08f, 0.18f),
                gravity: Random.Range(1.8f, 3.5f),
                stretch: true);
        }
    }
}
