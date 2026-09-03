#if SOCIAL_AURA_DIAG
using System;

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Abs(float v) => v < 0f ? -v : v;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static int RoundToInt(float v) => (int)Math.Round(v);
        public static bool Approximately(float a, float b) => Abs(a - b) < 1e-5f;
    }

    public static class Random
    {
        static System.Random _r = new System.Random(1);
        public static float value => (float)_r.NextDouble();
        public static int Range(int minInclusive, int maxExclusive) => _r.Next(minInclusive, maxExclusive);
    }

    public static class Application
    {
        public static string dataPath =>
            PathToAssets();

        static string PathToAssets()
        {
            string root = Environment.GetEnvironmentVariable("SOCIAL_AURA_PROJECT_ROOT");
            if (string.IsNullOrEmpty(root))
                root = "/Users/esbenhjort/Documents/Projects/Tilemap_graphics_test";
            return System.IO.Path.Combine(root, "Assets");
        }
    }

    public static class Debug
    {
        public static void Log(object msg) => Console.WriteLine(msg);
        public static void LogWarning(object msg) => Console.WriteLine("WARN: " + msg);
        public static void LogError(object msg) => Console.Error.WriteLine("ERR: " + msg);
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static float Distance(Vector2 a, Vector2 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }

    public static class Time
    {
        public static float unscaledTime { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string header) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max) { }
    }
}
#endif
