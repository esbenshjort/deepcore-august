using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Result of probing whether a task currently has valid work in the world.</summary>
    public readonly struct WorkAvailabilityResult
    {
        public readonly bool Available;
        public readonly string Reason;
        public readonly Vector2 HintWorld;
        public readonly float Urgency01;

        public WorkAvailabilityResult(bool available, string reason, Vector2 hint = default, float urgency01 = 0f)
        {
            Available = available;
            Reason = reason ?? "";
            HintWorld = hint;
            Urgency01 = urgency01;
        }

        public static WorkAvailabilityResult None(string reason) =>
            new WorkAvailabilityResult(false, reason);

        public static WorkAvailabilityResult Yes(string reason, Vector2 hint = default, float urgency = 0.5f) =>
            new WorkAvailabilityResult(true, reason, hint, urgency);
    }

    /// <summary>
    /// Probes existing systems for work — does not create work.
    /// Injected context from runner (hosts, collapse, camp).
    /// </summary>
    public sealed class WorkAvailabilityContext
    {
        public FineTerrainWorld World;
        public TunnelCollapseSystem Collapse;
        public MineInfrastructure Infra;
        public FreeWorkerController Excavator;
        public HaulerPerson Hauler;
        public RefinerPerson Refiner;
        public EngineerPerson Engineer;
        public ProspectorPerson Prospector;
        public ProspectorAnomalyAnalyst Analyst;
        public StewardPerson Steward;
        public CampLifeState Camp;
        public WorkerRuntime[] Crew;
        public System.Func<WorkerRuntime, Vector2> WorldPosOf;
        public Vector2 CampWorld;
        /// <summary>True after tonight's meal was served — blocks Prepare Meals availability.</summary>
        public bool MealServedTonight;

        public WorkAvailabilityResult Probe(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return WorkAvailabilityResult.None("no task");
            return taskId switch
            {
                WorkerGenericTaskIds.Rescue => ProbeRescue(),
                WorkerGenericTaskIds.TreatInjuries => ProbeTreat(),
                WorkerGenericTaskIds.ClearDebris => ProbeDebris(),
                WorkerGenericTaskIds.InstallSupports => ProbeSupports(),
                WorkerGenericTaskIds.InstallLighting => ProbeLighting(),
                WorkerGenericTaskIds.RepairEquipment => ProbeRepair(),
                WorkerGenericTaskIds.Excavate => ProbeExcavate(),
                WorkerGenericTaskIds.Prospect => ProbeProspect(),
                WorkerGenericTaskIds.AnalyseSurvey => ProbeAnalyse(),
                WorkerGenericTaskIds.HaulMaterials => ProbeHaul(),
                WorkerGenericTaskIds.RefineOre => ProbeRefine(),
                WorkerGenericTaskIds.PrepareMeals => ProbeCampDuty("meals"),
                WorkerGenericTaskIds.CleanCamp => ProbeCampDuty("clean"),
                WorkerGenericTaskIds.MaintainHygiene => ProbeCampDuty("hygiene"),
                WorkerGenericTaskIds.TendCampSystems => ProbeCampDuty("systems"),
                _ => WorkAvailabilityResult.None("unknown task"),
            };
        }

        WorkAvailabilityResult ProbeRescue()
        {
            if (Crew == null || Collapse == null) return WorkAvailabilityResult.None("no crew");
            for (int i = 0; i < Crew.Length; i++)
            {
                var wr = Crew[i];
                if (wr != null && WorkerRescue.NeedsRescue(wr))
                {
                    Vector2 p = WorldPosOf != null ? WorldPosOf(wr) : CampWorld;
                    return WorkAvailabilityResult.Yes("casualty needs rescue", p, 1f);
                }
            }
            return WorkAvailabilityResult.None("no casualties");
        }

        WorkAvailabilityResult ProbeTreat()
        {
            if (Crew == null) return WorkAvailabilityResult.None("no crew");
            for (int i = 0; i < Crew.Length; i++)
            {
                var wr = Crew[i];
                if (wr == null || !wr.IsAlive || wr.State == null) continue;
                if (wr.State.Incapacitated) continue; // needs rescue first
                bool care = wr.State.NeedsCare
                            || (wr.CampBody != null && wr.CampBody.SeekingStewardCare);
                if (!care) continue;
                // Serious / off-duty / needs care — not every bruise
                var status = InjuryResponse.EvaluateWorkStatus(wr);
                if (status == WorkerInjuryWorkStatus.Fit && !wr.State.NeedsCare) continue;
                Vector2 p = WorldPosOf != null ? WorldPosOf(wr) : CampWorld;
                float urg = status == WorkerInjuryWorkStatus.OffDuty
                            || status == WorkerInjuryWorkStatus.Incapacitated ? 0.95f : 0.55f;
                return WorkAvailabilityResult.Yes("patient needs care", p, urg);
            }
            return WorkAvailabilityResult.None("no patients");
        }

        WorkAvailabilityResult ProbeDebris()
        {
            if (Collapse == null) return WorkAvailabilityResult.None("no collapse sys");
            var fields = Collapse.Fields;
            for (int i = 0; i < fields.Count; i++)
            {
                var f = fields[i];
                if (f == null || f.Cleared || !f.BlocksNav) continue;
                return WorkAvailabilityResult.Yes("blocking debris", f.Epicenter, 0.85f);
            }
            return WorkAvailabilityResult.None("no blocking debris");
        }

        WorkAvailabilityResult ProbeSupports()
        {
            if (Infra == null || Excavator == null)
                return WorkAvailabilityResult.None("no infra");
            Vector2 from = Excavator.Position;
            if (Infra.TryPickNextSupportCell(from, criticalOnly: true, out var cell, out _))
                return WorkAvailabilityResult.Yes("critical support", InfraCell(cell), 0.9f);
            if (Infra.TryPickNextSupportCell(from, criticalOnly: false, out cell, out _))
                return WorkAvailabilityResult.Yes("preventative support", InfraCell(cell), 0.55f);
            return WorkAvailabilityResult.None("no support sites");
        }

        WorkAvailabilityResult ProbeLighting()
        {
            if (Infra == null || Excavator == null)
                return WorkAvailabilityResult.None("no infra");
            if (Infra.TryPickNextLanternCell(Excavator.Position, out var cell, out _))
                return WorkAvailabilityResult.Yes("dark area", InfraCell(cell), 0.6f);
            return WorkAvailabilityResult.None("no lantern sites");
        }

        WorkAvailabilityResult ProbeRepair()
        {
            if (Excavator == null) return WorkAvailabilityResult.None("no excavator");
            // Engineer repair when overheated / damaged — soft signal via heat
            if (Excavator.IsOverheated)
                return WorkAvailabilityResult.Yes("excavator overheated", Excavator.Position, 0.7f);
            if (Engineer != null && Engineer.WorkKind == EngineerWorkKind.Repairing)
                return WorkAvailabilityResult.Yes("repair in progress", Excavator.Position, 0.65f);
            return WorkAvailabilityResult.None("no repair needed");
        }

        WorkAvailabilityResult ProbeExcavate()
        {
            if (Excavator == null) return WorkAvailabilityResult.None("excavator unavailable");
            var plan = Excavator.PaintPlan;
            if (plan != null && plan.PendingDigCount > 0)
                return WorkAvailabilityResult.Yes("painted dig cells", Excavator.Position, 0.75f);
            if (Excavator.HasGoal)
                return WorkAvailabilityResult.Yes("dig route active", Excavator.Position, 0.7f);
            return WorkAvailabilityResult.None("no painted excavation");
        }

        WorkAvailabilityResult ProbeProspect()
        {
            if (Prospector == null) return WorkAvailabilityResult.None("no prospector host");
            // Player-created field work (not Manual idle)
            if (Prospector.WorkMode != ProspectorWorkMode.Manual)
                return WorkAvailabilityResult.Yes("field prospect mode", Prospector.Position, 0.55f);
            // Player placed scanner — travel/setup is valid Prospect work
            if (Prospector.HasScannerAssignment || Prospector.IsSettingUpScanner)
                return WorkAvailabilityResult.Yes("scanner setup", Prospector.Position, 0.6f);
            // Manual with no player order: Priority must NOT invent scans
            return WorkAvailabilityResult.None("Manual — no player prospect order");
        }

        WorkAvailabilityResult ProbeAnalyse()
        {
            if (Analyst != null && Analyst.HasWork)
                return WorkAvailabilityResult.Yes("survey analysis queue", CampWorld, 0.5f);
            return WorkAvailabilityResult.None("no analysis queue");
        }

        WorkAvailabilityResult ProbeHaul()
        {
            if (LoosePile.LiveCount > 0)
                return WorkAvailabilityResult.Yes("loose material", CampWorld, 0.55f);
            // Mid-haul cargo is still valid work — do not invalidate Return/Deposit
            if (Hauler != null && Hauler.CargoCount > 0)
                return WorkAvailabilityResult.Yes("cargo in cart", CampWorld, 0.5f);
            return WorkAvailabilityResult.None("no haul piles");
        }

        WorkAvailabilityResult ProbeRefine()
        {
            if (Refiner == null) return WorkAvailabilityResult.None("no refiner");
            if (Refiner.CanFetchOre())
                return WorkAvailabilityResult.Yes("ore to refine", Refiner.transform.localPosition, 0.55f);
            // Mid-wash / carry still counts as available refine work
            string lab = Refiner.ActivityLabel ?? "";
            if (lab.IndexOf("IDLE", System.StringComparison.OrdinalIgnoreCase) < 0
                && lab.IndexOf("CONSULT", System.StringComparison.OrdinalIgnoreCase) < 0
                && lab.IndexOf("MEET", System.StringComparison.OrdinalIgnoreCase) < 0)
                return WorkAvailabilityResult.Yes("refine in progress", Refiner.transform.localPosition, 0.45f);
            return WorkAvailabilityResult.None("no ore to refine");
        }

        WorkAvailabilityResult ProbeCampDuty(string kind)
        {
            if (Camp == null) return WorkAvailabilityResult.None("no camp");
            if (kind == "clean")
            {
                if (Camp.Hygiene01 < 0.55f)
                    return WorkAvailabilityResult.Yes("camp dirty", CampWorld, 0.65f);
                if (Steward != null && Steward.WorkKind == StewardWorkKind.CleaningCamp)
                    return WorkAvailabilityResult.Yes("cleaning in progress", CampWorld, 0.5f);
                return WorkAvailabilityResult.None("camp clean enough");
            }
            if (kind == "meals")
            {
                if (MealServedTonight || Camp.MealServedTonight)
                    return WorkAvailabilityResult.None("meal already served");
                if (Steward != null && (Steward.WorkKind == StewardWorkKind.PreparingMeal
                                        || Steward.WorkKind == StewardWorkKind.KitchenDuty))
                    return WorkAvailabilityResult.Yes("meal duty active", CampWorld, 0.55f);
                // Real opportunity: meal not served — Steward PickDuty can prepare (no invented meal)
                return WorkAvailabilityResult.Yes("meal not yet served", CampWorld, 0.4f);
            }
            if (kind == "hygiene")
            {
                // No distinct Maintain Hygiene executor beyond toilet (hard override) / clean camp
                return WorkAvailabilityResult.None("no distinct hygiene work target");
            }
            // Tend camp systems — only when steward is on post-meal/kitchen systems work
            if (Steward != null && (Steward.WorkKind == StewardWorkKind.AfterMealCleanup
                                    || Steward.WorkKind == StewardWorkKind.KitchenDuty))
                return WorkAvailabilityResult.Yes("camp systems duty", CampWorld, 0.45f);
            return WorkAvailabilityResult.None("no camp systems work");
        }

        Vector2 InfraCell(Vector2Int cell) =>
            World != null ? World.CellCenter(cell.x, cell.y) : CampWorld;
    }
}
