using System;
using System.Collections.Generic;

namespace DeepCore.FreeMovement
{
    /// <summary>Universal work task ids — same list for every worker. Not JobType.</summary>
    public static class WorkerGenericTaskIds
    {
        public const string Rescue = "task.rescue";
        public const string TreatInjuries = "task.treat";
        public const string ClearDebris = "task.clear_debris";
        public const string InstallSupports = "task.supports";
        public const string InstallLighting = "task.lighting";
        public const string RepairEquipment = "task.repair";
        public const string Excavate = "task.excavate";
        public const string Prospect = "task.prospect";
        public const string AnalyseSurvey = "task.analyse";
        public const string HaulMaterials = "task.haul";
        public const string RefineOre = "task.refine";
        public const string PrepareMeals = "task.meals";
        public const string CleanCamp = "task.clean";
        public const string MaintainHygiene = "task.hygiene";
        public const string TendCampSystems = "task.camp_systems";
    }

    public enum WorkTaskCategory : byte
    {
        Emergency = 0,
        Infrastructure = 1,
        Production = 2,
        Camp = 3,
    }

    /// <summary>1 = highest … 4 = lowest; 0 = OFF (will not voluntarily select).</summary>
    public enum WorkPriorityLevel : byte
    {
        Off = 0,
        P1 = 1,
        P2 = 2,
        P3 = 3,
        P4 = 4,
    }

    public sealed class WorkTaskDefinition
    {
        public string Id;
        public string DisplayName;
        public string ShortName;
        public WorkTaskCategory Category;
        public WorkPriorityLevel DefaultPriority = WorkPriorityLevel.P4;
        public bool EmergencyCapable;
        public bool SupportsHourTarget = true;
        public bool RequiresTool;
        public bool RequiresStation;
        public bool RequiresTarget = true;
        public bool PlayerOrderDriven;
        public string Tooltip;
        /// <summary>Optional specialization familiarity (efficiency nudge only — never permission).</summary>
        public JobType FamiliarJob = JobType.Unassigned;
    }

    /// <summary>Single shared task registry. Adding a task = add definition + availability + executor.</summary>
    public static class WorkTaskRegistry
    {
        static readonly WorkTaskDefinition[] Tasks =
        {
            Def(WorkerGenericTaskIds.Rescue, "Rescue", "RESCUE", WorkTaskCategory.Emergency,
                WorkPriorityLevel.P1, emergency: true, hourTarget: false, tool: false, station: false,
                "Recover incapacitated workers. Existing rescue system. Any mobile worker may perform.",
                JobType.Unassigned),
            Def(WorkerGenericTaskIds.TreatInjuries, "Treat Injuries", "TREAT", WorkTaskCategory.Emergency,
                WorkPriorityLevel.P3, emergency: true, hourTarget: false, tool: false, station: false,
                "Serious wound care at camp. Reuses Steward treatment. Minor scrapes are not emergencies.",
                JobType.Steward),

            Def(WorkerGenericTaskIds.ClearDebris, "Clear Debris", "CLEAR DEBRIS", WorkTaskCategory.Infrastructure,
                WorkPriorityLevel.P3, emergency: false, hourTarget: true, tool: false, station: false,
                "Clear collapse rubble blocking tunnels. World debris authority. No silent delete.",
                JobType.Hauling),
            Def(WorkerGenericTaskIds.InstallSupports, "Install Supports", "SUPPORTS", WorkTaskCategory.Infrastructure,
                WorkPriorityLevel.P3, emergency: false, hourTarget: true, tool: false, station: true,
                "Structural support work. Existing MineInfrastructure creates targets.",
                JobType.Engineering),
            Def(WorkerGenericTaskIds.InstallLighting, "Install Lighting", "LIGHTING", WorkTaskCategory.Infrastructure,
                WorkPriorityLevel.P3, emergency: false, hourTarget: true, tool: false, station: true,
                "Lantern placement. Existing infrastructure pickers create targets.",
                JobType.Engineering),
            Def(WorkerGenericTaskIds.RepairEquipment, "Repair Equipment", "REPAIR", WorkTaskCategory.Infrastructure,
                WorkPriorityLevel.P3, emergency: false, hourTarget: true, tool: false, station: true,
                "Repair excavator / equipment. Existing engineer repair flow.",
                JobType.Engineering),

            Def(WorkerGenericTaskIds.Excavate, "Excavate", "EXCAVATE", WorkTaskCategory.Production,
                WorkPriorityLevel.P3, emergency: false, hourTarget: true, tool: true, station: true,
                "Dig player-painted cells only. Never invents excavation routes. Requires excavator.",
                JobType.Excavation, playerOrder: true),
            Def(WorkerGenericTaskIds.Prospect, "Prospect", "PROSPECT", WorkTaskCategory.Production,
                WorkPriorityLevel.P3, emergency: false, hourTarget: true, tool: true, station: true,
                "Field prospecting when player has created work (scan modes / scanner place). Manual idle = no work. Does not invent scans.",
                JobType.Prospecting),
            Def(WorkerGenericTaskIds.AnalyseSurvey, "Analyse Survey Data", "ANALYSE", WorkTaskCategory.Production,
                WorkPriorityLevel.P3, emergency: false, hourTarget: true, tool: false, station: true,
                "Desk analysis of survey / anomaly data. Existing analyst queue.",
                JobType.Prospecting),
            Def(WorkerGenericTaskIds.HaulMaterials, "Haul Materials", "HAUL", WorkTaskCategory.Production,
                WorkPriorityLevel.P3, emergency: false, hourTarget: true, tool: false, station: true,
                "Carry loose material to stockpiles. Existing haul targets.",
                JobType.Hauling),
            Def(WorkerGenericTaskIds.RefineOre, "Refine Ore", "REFINE", WorkTaskCategory.Production,
                WorkPriorityLevel.P3, emergency: false, hourTarget: true, tool: false, station: true,
                "Wash / refine stockpiled ore. Existing refiner station.",
                JobType.Refining),

            Def(WorkerGenericTaskIds.PrepareMeals, "Prepare Meals", "MEALS", WorkTaskCategory.Camp,
                WorkPriorityLevel.P3, emergency: false, hourTarget: true, tool: false, station: true,
                "Camp kitchen duty. Existing Steward camp life.",
                JobType.Steward),
            Def(WorkerGenericTaskIds.CleanCamp, "Clean Camp", "CLEAN", WorkTaskCategory.Camp,
                WorkPriorityLevel.P3, emergency: false, hourTarget: true, tool: false, station: true,
                "Camp hygiene cleaning. Existing Steward camp life.",
                JobType.Steward),
            Def(WorkerGenericTaskIds.MaintainHygiene, "Maintain Hygiene", "HYGIENE", WorkTaskCategory.Camp,
                WorkPriorityLevel.P4, emergency: false, hourTarget: true, tool: false, station: false,
                "Reserved. Toilet urgency is a hard personal override, not Priority work. No distinct hygiene executor yet.",
                JobType.Steward),
            Def(WorkerGenericTaskIds.TendCampSystems, "Tend Camp Systems", "CAMP SYS", WorkTaskCategory.Camp,
                WorkPriorityLevel.P4, emergency: false, hourTarget: true, tool: false, station: true,
                "Active when Steward is on kitchen / after-meal cleanup. No invented camp busywork.",
                JobType.Steward),
        };

        static WorkTaskDefinition Def(
            string id, string name, string shortName, WorkTaskCategory cat,
            WorkPriorityLevel defP, bool emergency, bool hourTarget, bool tool, bool station,
            string tip, JobType familiar, bool playerOrder = false)
        {
            return new WorkTaskDefinition
            {
                Id = id,
                DisplayName = name,
                ShortName = shortName,
                Category = cat,
                DefaultPriority = defP,
                EmergencyCapable = emergency,
                SupportsHourTarget = hourTarget,
                RequiresTool = tool,
                RequiresStation = station,
                PlayerOrderDriven = playerOrder,
                Tooltip = tip,
                FamiliarJob = familiar,
            };
        }

        public static IReadOnlyList<WorkTaskDefinition> All => Tasks;

        public static WorkTaskDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Tasks.Length; i++)
                if (Tasks[i].Id == id) return Tasks[i];
            return null;
        }

        /// <summary>
        /// Profession-scoped task list for Assign / Work Priorities UI.
        /// FamiliarJob match only — never a permission gate for the resolver.
        /// </summary>
        public static void CollectForJob(JobType job, List<WorkTaskDefinition> into)
        {
            into.Clear();
            if (job == JobType.Unassigned) return;
            for (int i = 0; i < Tasks.Length; i++)
            {
                var t = Tasks[i];
                if (t.FamiliarJob == job)
                    into.Add(t);
            }
        }

        public static string CategoryLabel(WorkTaskCategory c) => c switch
        {
            WorkTaskCategory.Emergency => "EMERGENCY",
            WorkTaskCategory.Infrastructure => "INFRASTRUCTURE",
            WorkTaskCategory.Production => "PRODUCTION",
            WorkTaskCategory.Camp => "CAMP",
            _ => "—",
        };
    }
}
