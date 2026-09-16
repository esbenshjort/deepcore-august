using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Per-task priority + optional shift hour target. Person-owned.</summary>
    [Serializable]
    public sealed class WorkerTaskPriorityEntry
    {
        public string TaskId;
        public WorkPriorityLevel Priority = WorkPriorityLevel.P4;
        /// <summary>Planned hours this shift (0 = no target).</summary>
        public float TargetHours;
        /// <summary>Accumulated work time this shift (reset at shift boundary).</summary>
        public float WorkedHoursThisShift;
    }

    /// <summary>
    /// Worker priority configuration + shift work clocks.
    /// Same task list for every worker — JobType never gates presence.
    /// </summary>
    [Serializable]
    public sealed class WorkerPriorityPrefs
    {
        public const float HourStep = 0.5f;
        public const float MinCommitGameHours = 0.35f;

        readonly Dictionary<string, WorkerTaskPriorityEntry> _byId = new(24);

        /// <summary>Currently resolved / executing generic task id (empty if none).</summary>
        public string ActiveTaskId = "";
        public string LastResolveReason = "";
        public string LastUnavailableReason = "";
        public float ActiveTaskStartedGameHours = -1f;
        public float Suitability01;

        // DEV / diagnostics — last selected score breakdown (not gameplay)
        public float LastScoreTotal;
        public float LastScorePriority;
        public float LastScoreDeficit;
        public float LastScoreSuit;
        public float LastScoreDistance;
        public float LastScoreCondition;
        public float LastScoreContinuity;
        public float LastScoreEmergency;
        public string LastTargetHint = "";
        public bool LastEmergencyOverride;

        /// <summary>Set when player changes priority/target — forces immediate resolve.</summary>
        public bool ResolverDirty;
        /// <summary>Last validation of ActiveTaskId (DEV + OFF authority).</summary>
        public bool ActiveTaskValid = true;
        public string ActiveInvalidationReason = "";
        /// <summary>True after OFF forced host yield this invalidate pass.</summary>
        public bool LastHostYieldedForOff;
        /// <summary>Winning hard priority band from last resolve (1–4), 0 if none.</summary>
        public int LastWinningPriorityBand;
        /// <summary>DEV: why a lower-band current task was rejected.</summary>
        public string LastBandRejectHint = "";

        public WorkerTaskPriorityEntry GetOrCreate(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return null;
            if (_byId.TryGetValue(taskId, out var e)) return e;
            var def = WorkTaskRegistry.Get(taskId);
            e = new WorkerTaskPriorityEntry
            {
                TaskId = taskId,
                Priority = def != null ? def.DefaultPriority : WorkPriorityLevel.P4,
                TargetHours = 0f,
            };
            _byId[taskId] = e;
            return e;
        }

        public WorkPriorityLevel GetPriority(string taskId) =>
            GetOrCreate(taskId)?.Priority ?? WorkPriorityLevel.P4;

        public void SetPriority(string taskId, WorkPriorityLevel p)
        {
            var e = GetOrCreate(taskId);
            if (e == null) return;
            if (e.Priority == p) return;
            e.Priority = p;
            OnPriorityConfigChanged(taskId);
        }

        public void CyclePriority(string taskId, bool reverse)
        {
            var e = GetOrCreate(taskId);
            if (e == null) return;
            int v = (int)e.Priority;
            if (!reverse)
            {
                // 1→2→3→4→OFF→1
                if (v == 0) v = 1;
                else if (v >= 4) v = 0;
                else v++;
            }
            else
            {
                // reverse: 1→OFF→4→3→2→1
                if (v <= 1) v = v == 1 ? 0 : 1;
                else if (v == 0) v = 4;
                else v--;
            }
            e.Priority = (WorkPriorityLevel)v;
            OnPriorityConfigChanged(taskId);
        }

        void OnPriorityConfigChanged(string taskId)
        {
            ResolverDirty = true;
            if (!string.IsNullOrEmpty(ActiveTaskId) && ActiveTaskId == taskId
                && GetPriority(taskId) == WorkPriorityLevel.Off)
            {
                ActiveTaskValid = false;
                ActiveInvalidationReason = "priority OFF";
            }
        }

        public void ClearActiveTask(string reason)
        {
            ActiveTaskId = "";
            ActiveTaskStartedGameHours = -1f;
            ActiveTaskValid = true;
            ActiveInvalidationReason = reason ?? "";
            LastResolveReason = reason ?? "";
        }

        public float GetTargetHours(string taskId) => GetOrCreate(taskId)?.TargetHours ?? 0f;

        public void SetTargetHours(string taskId, float hours)
        {
            var e = GetOrCreate(taskId);
            if (e == null) return;
            var def = WorkTaskRegistry.Get(taskId);
            if (def != null && !def.SupportsHourTarget)
            {
                e.TargetHours = 0f;
                return;
            }
            float next = Mathf.Clamp(Mathf.Round(hours / HourStep) * HourStep, 0f, 16f);
            if (Mathf.Abs(e.TargetHours - next) < 0.001f) return;
            e.TargetHours = next;
            ResolverDirty = true;
        }

        public void AdjustTargetHours(string taskId, float delta) =>
            SetTargetHours(taskId, GetTargetHours(taskId) + delta);

        public float GetWorkedHours(string taskId) => GetOrCreate(taskId)?.WorkedHoursThisShift ?? 0f;

        public void AccrueWork(string taskId, float gameHours)
        {
            if (gameHours <= 0f || string.IsNullOrEmpty(taskId)) return;
            var e = GetOrCreate(taskId);
            if (e != null) e.WorkedHoursThisShift += gameHours;
        }

        public void ResetShiftAccumulation()
        {
            foreach (var kv in _byId)
                kv.Value.WorkedHoursThisShift = 0f;
            ActiveTaskStartedGameHours = -1f;
        }

        public float TargetDeficit(string taskId)
        {
            var e = GetOrCreate(taskId);
            if (e == null || e.TargetHours <= 0.01f) return 0f;
            return Mathf.Max(0f, e.TargetHours - e.WorkedHoursThisShift);
        }

        public bool IsOff(string taskId) => GetPriority(taskId) == WorkPriorityLevel.Off;

        public void EnsureAllTasksRegistered()
        {
            var all = WorkTaskRegistry.All;
            for (int i = 0; i < all.Count; i++)
                GetOrCreate(all[i].Id);
        }

        /// <summary>Prototype crew specialization presets — editable defaults, not locks.</summary>
        public static void ApplyCrewDefaults(WorkerRuntime wr, string displayName)
        {
            if (wr == null) return;
            if (wr.Priorities == null) wr.Priorities = new WorkerPriorityPrefs();
            var p = wr.Priorities;
            p.EnsureAllTasksRegistered();
            // Soft baseline: everything P3–P4, Rescue P1
            SetAll(p, WorkPriorityLevel.P4);
            p.SetPriority(WorkerGenericTaskIds.Rescue, WorkPriorityLevel.P1);
            p.SetPriority(WorkerGenericTaskIds.HaulMaterials, WorkPriorityLevel.P3);
            p.SetPriority(WorkerGenericTaskIds.TreatInjuries, WorkPriorityLevel.P3);

            switch (displayName)
            {
                case "Lewis":
                    p.SetPriority(WorkerGenericTaskIds.Prospect, WorkPriorityLevel.P1);
                    p.SetPriority(WorkerGenericTaskIds.AnalyseSurvey, WorkPriorityLevel.P1);
                    p.SetPriority(WorkerGenericTaskIds.HaulMaterials, WorkPriorityLevel.P3);
                    break;
                case "Mara":
                    p.SetPriority(WorkerGenericTaskIds.Excavate, WorkPriorityLevel.P1);
                    p.SetPriority(WorkerGenericTaskIds.ClearDebris, WorkPriorityLevel.P2);
                    p.SetPriority(WorkerGenericTaskIds.HaulMaterials, WorkPriorityLevel.P3);
                    break;
                case "Kowalski":
                    p.SetPriority(WorkerGenericTaskIds.HaulMaterials, WorkPriorityLevel.P1);
                    p.SetPriority(WorkerGenericTaskIds.ClearDebris, WorkPriorityLevel.P1);
                    break;
                case "Elena":
                    p.SetPriority(WorkerGenericTaskIds.RefineOre, WorkPriorityLevel.P1);
                    p.SetPriority(WorkerGenericTaskIds.AnalyseSurvey, WorkPriorityLevel.P2);
                    break;
                case "Viktor":
                    p.SetPriority(WorkerGenericTaskIds.InstallSupports, WorkPriorityLevel.P1);
                    p.SetPriority(WorkerGenericTaskIds.InstallLighting, WorkPriorityLevel.P1);
                    p.SetPriority(WorkerGenericTaskIds.RepairEquipment, WorkPriorityLevel.P1);
                    p.SetPriority(WorkerGenericTaskIds.ClearDebris, WorkPriorityLevel.P2);
                    p.SetTargetHours(WorkerGenericTaskIds.InstallSupports, 4f);
                    p.SetTargetHours(WorkerGenericTaskIds.InstallLighting, 3f);
                    p.SetTargetHours(WorkerGenericTaskIds.RepairEquipment, 1f);
                    break;
                case "Kit":
                    p.SetPriority(WorkerGenericTaskIds.TreatInjuries, WorkPriorityLevel.P1);
                    p.SetPriority(WorkerGenericTaskIds.PrepareMeals, WorkPriorityLevel.P1);
                    p.SetPriority(WorkerGenericTaskIds.CleanCamp, WorkPriorityLevel.P1);
                    p.SetPriority(WorkerGenericTaskIds.MaintainHygiene, WorkPriorityLevel.P1);
                    p.SetPriority(WorkerGenericTaskIds.TendCampSystems, WorkPriorityLevel.P1);
                    break;
            }
        }

        static void SetAll(WorkerPriorityPrefs p, WorkPriorityLevel level)
        {
            var all = WorkTaskRegistry.All;
            for (int i = 0; i < all.Count; i++)
                p.SetPriority(all[i].Id, level);
        }

        public string PriorityLabel(WorkPriorityLevel p) => p == WorkPriorityLevel.Off ? "OFF" : ((int)p).ToString();
    }
}
