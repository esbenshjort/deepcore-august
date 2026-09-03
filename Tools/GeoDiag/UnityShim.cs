#if GEO_DIAG_CONSOLE
using System;

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Abs(float v) => v < 0f ? -v : v;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int RoundToInt(float v) => (int)Math.Round(v);
        public static bool Approximately(float a, float b) => Abs(a - b) < 1e-5f;
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static float Distance(Vector2 a, Vector2 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }

    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1, 1);
    }

    public static class Application
    {
        public static string dataPath =>
            "/Users/esbenhjort/Documents/Projects/Tilemap_graphics_test/Assets";
    }

    public static class Debug
    {
        public static void Log(object msg) { Console.WriteLine(msg); }
        public static void LogWarning(object msg) { Console.WriteLine("WARN: " + msg); }
        public static void LogError(object msg) { Console.Error.WriteLine("ERR: " + msg); }
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
