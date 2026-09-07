using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// TEMP DEV: catch any large runtime position jump (teleport / soft-arrive / snap).
    /// Locomotion steps below the threshold are ignored.
    /// </summary>
    public static class WorkerRelocationLog
    {
        public const float ReportMinDistance = 0.55f;

        public static string LastWho = "—";
        public static string LastReason = "NONE";
        public static string LastSource = "";
        public static Vector2 LastFrom;
        public static Vector2 LastTo;
        public static float LastRealtime = -1f;

        public static void Report(string who, Vector2 from, Vector2 to, string reason, string source)
        {
            float d = Vector2.Distance(from, to);
            if (d < ReportMinDistance) return;
            LastWho = string.IsNullOrEmpty(who) ? "?" : who;
            LastReason = reason ?? "";
            LastSource = source ?? "";
            LastFrom = from;
            LastTo = to;
            LastRealtime = Time.realtimeSinceStartup;
            string line =
                $"[WORKER RELOCATION] {LastWho} from:({from.x:0.00},{from.y:0.00}) "
                + $"to:({to.x:0.00},{to.y:0.00}) d={d:0.00} reason:{LastReason} source:{LastSource}";
            DigHoodLog.Push(line);
            Debug.LogWarning(line);
        }

        public static string LastSummary()
        {
            if (LastRealtime < 0f || LastReason == "NONE")
                return "NONE";
            return $"{LastWho} | {LastReason} | {LastSource}";
        }
    }
}
