using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum WorkerFootingEvent : byte
    {
        None = 0,
        BriefSlow = 1,
        Stumble = 2,
        LossOfFooting = 3,
    }

    /// <summary>
    /// Person-owned transient footing / walk readout. Follows WorkerRuntime across jobs.
    /// </summary>
    public sealed class WorkerLocomotionState
    {
        public float SlowMul = 1f;
        public float SlowUntilUnscaled;
        public float NextCheckUnscaled;
        public WorkerFootingEvent LastEvent;
        public float LastEventUnscaled;
        public string LastEventDetail = "";
        public float LastEffectiveSpeed;
        public WorkerTerrainKind LastTerrain;
        public float LastDifficulty01;
        public float LastInjuryMul = 1f;
        public float LastStaminaMul = 1f;
        public float LastLoadMul = 1f;
        public float LastTerrainMul = 1f;
        public float ExposureAccum;
        public WorkerAccidentReport LastAccident;

        public void ClearEvent()
        {
            LastEvent = WorkerFootingEvent.None;
            LastEventDetail = "";
        }
    }

    /// <summary>
    /// Universal person walking physics. Job hosts call this for on-foot movement;
    /// excavator chassis keeps separate machine speeds.
    /// </summary>
    public static class WorkerLocomotion
    {
        /// <summary>Minimum real seconds between footing checks while moving on hard ground.</summary>
        public const float FootingCheckInterval = 0.85f;

        /// <summary>Difficulty below this never rolls footing mistakes.</summary>
        public const float StumbleDifficultyMin = 0.35f;

        /// <summary>Exposure units needed before a discrete accident check.</summary>
        public const float ExposurePerCheck = 1f;

        /// <summary>
        /// Optional clock for offline audits (Unity's Time.unscaledTime is read-only).
        /// Null = use live Time.unscaledTime.
        /// </summary>
        public static float? AuditUnscaledTime;

        static float NowUnscaled => AuditUnscaledTime ?? Time.unscaledTime;

        public static void AdvanceAuditClock(float delta)
        {
            AuditUnscaledTime = (AuditUnscaledTime ?? 0f) + delta;
        }

        public static void ClearAuditClock() => AuditUnscaledTime = null;

        public static WorkerPhysicalProfile ProfileOf(WorkerRuntime wr) =>
            WorkerPhysicalProfile.From(wr);

        /// <summary>
        /// Effective walk speed (world units/sec).
        /// <paramref name="roleBias"/> is a modest job intent scale (clamped).
        /// Hosts that pass absoluteSpeed/ReferenceWalkSpeed are clamped so they cannot sprint.
        /// </summary>
        public static float EvaluateWalkSpeed(
            WorkerRuntime wr,
            in WorkerTerrainSample terrain,
            float roleBias = 1f,
            float carriedLoad01 = 0f,
            bool isMoving = true,
            float deltaTime = 0f)
        {
            var loco = wr?.Locomotion;
            var profile = WorkerPhysicalProfile.From(wr);
            float bias = Mathf.Clamp(roleBias,
                WorkerPhysicalProfile.RoleBiasMin, WorkerPhysicalProfile.RoleBiasMax);
            float speed = profile.MoveSpeed * bias;

            float terrainMul = TerrainSpeedMul(profile, terrain);
            float injuryMul = InjuryMoveMul(wr);
            float staminaMul = StaminaMoveMul(wr);
            float loadMul = LoadMoveMul(profile, carriedLoad01);
            if (wr?.Injuries != null && carriedLoad01 > 0.01f)
                loadMul *= WorkerInjuryConsequences.LoadCarryMul(wr.Injuries);

            speed *= terrainMul * injuryMul * staminaMul * loadMul;
            // Tiny accel feel — was a hidden speed bump; keep near-neutral
            speed *= Mathf.Lerp(0.97f, 1f, Mathf.Clamp01((profile.AccelerationMul - 0.72f) / 0.56f));

            // Hard walk ceiling — off-duty / role bias cannot exceed believable walk
            speed = Mathf.Min(speed, WorkerPhysicalProfile.MaxNormalWalkSpeed);

            if (loco != null)
            {
                TickFootingRecovery(loco, profile, deltaTime);
                if (isMoving && terrain.Difficulty01 >= StumbleDifficultyMin && deltaTime > 0f)
                    MaybeFootingEvent(wr, loco, profile, terrain, carriedLoad01, deltaTime);

                if (NowUnscaled < loco.SlowUntilUnscaled)
                    speed *= Mathf.Clamp(loco.SlowMul, 0.15f, 1f);

                loco.LastEffectiveSpeed = speed;
                loco.LastTerrain = terrain.Kind;
                loco.LastDifficulty01 = terrain.Difficulty01;
                loco.LastInjuryMul = injuryMul;
                loco.LastStaminaMul = staminaMul;
                loco.LastLoadMul = loadMul;
                loco.LastTerrainMul = terrainMul;
            }

            return Mathf.Max(0.06f, speed);
        }

        /// <summary>
        /// Convert a legacy absolute host speed into a clamped role bias.
        /// Prefer passing 1f from new call sites.
        /// </summary>
        public static float RoleBiasFromAbsolute(float absoluteWorldSpeed) =>
            absoluteWorldSpeed / Mathf.Max(0.01f, WorkerPhysicalProfile.ReferenceWalkSpeed);

        public static float WalkSpeedAt(
            WorkerRuntime wr,
            FineTerrainWorld world,
            Vector2 worldPos,
            float bodyRadius,
            float roleBias = 1f,
            float carriedLoad01 = 0f,
            bool isMoving = true)
        {
            var terrain = WorkerTerrainSampler.Sample(world, worldPos, bodyRadius);
            return EvaluateWalkSpeed(
                wr, terrain, roleBias, carriedLoad01, isMoving, Time.deltaTime);
        }

        public static float TerrainSpeedMul(in WorkerPhysicalProfile profile, in WorkerTerrainSample terrain)
        {
            if (terrain.Difficulty01 <= 0.001f) return 1f;

            float baseMul = terrain.Kind switch
            {
                WorkerTerrainKind.LooseRock => 0.48f,
                WorkerTerrainKind.Rough => 0.78f,
                WorkerTerrainKind.SteepUneven => 0.70f,
                WorkerTerrainKind.Rubble => 0.40f,
                WorkerTerrainKind.WetSlippery => 0.62f,
                _ => Mathf.Lerp(1f, 0.88f, terrain.Difficulty01),
            };

            float adapt = Mathf.Lerp(0.82f, 1.18f, Mathf.Clamp01((profile.TerrainAdapt - 0.5f) / 0.9f));
            float mul = baseMul * adapt;
            float floor = terrain.Kind == WorkerTerrainKind.Rubble ? 0.32f
                : terrain.Kind == WorkerTerrainKind.LooseRock ? 0.38f
                : terrain.Kind == WorkerTerrainKind.WetSlippery ? 0.48f
                : 0.55f;
            return Mathf.Clamp(mul, floor, 1f);
        }

        /// <summary>Body-part injury consequences + soft meter floor — not one generic Injury mul.</summary>
        public static float InjuryMoveMul(WorkerRuntime wr)
        {
            float partMul = WorkerInjuryConsequences.WalkSpeedMul(wr?.Injuries);
            float meterMul = InjuryMeterSoftMul(wr?.State);
            return Mathf.Min(partMul, meterMul);
        }

        /// <summary>Legacy soft mul from aggregate meter (fights/overheat without typed record yet).</summary>
        public static float InjuryMeterSoftMul(WorkerState st)
        {
            if (st == null) return 1f;
            if (st.NeedsCare || st.Injury >= WorkerState.CriticalInjuryThreshold)
                return 0.78f;
            if (st.Injury >= WorkerState.CareInjuryThreshold)
                return 0.85f;
            if (st.Injury >= 40f)
                return Mathf.Lerp(0.94f, 0.85f, (st.Injury - 40f) / 20f);
            if (st.Injury >= 15f)
                return Mathf.Lerp(1f, 0.94f, (st.Injury - 15f) / 25f);
            return 1f;
        }

        public static float StaminaMoveMul(WorkerRuntime wr)
        {
            float r = 1f;
            if (wr?.State != null && wr.State.StaminaPrimed)
            {
                float max = wr.PhysicalStaminaMax;
                if (max > 0.001f)
                    r = Mathf.Clamp01(wr.State.PhysicalStamina / max);
            }
            const float tired = 0.30f;
            const float exhaust = 0.10f;
            float mul;
            if (r >= tired) mul = 1f;
            else if (r <= exhaust) mul = 0.82f;
            else
            {
                float t = (tired - r) / (tired - exhaust);
                mul = Mathf.Lerp(1f, 0.88f, Mathf.Clamp01(t));
            }
            var profile = WorkerPhysicalProfile.From(wr);
            if (mul < 0.999f)
                mul = Mathf.Lerp(mul, 1f, Mathf.Clamp01((profile.Endurance - 1f) * 0.35f));
            return Mathf.Clamp(mul, 0.72f, 1f);
        }

        public static float LoadMoveMul(in WorkerPhysicalProfile profile, float carriedLoad01)
        {
            float load = Mathf.Clamp01(carriedLoad01);
            if (load <= 0.001f) return 1f;
            float light = Mathf.Lerp(0.78f, 0.92f, Mathf.Clamp01((profile.LoadHandling - 0.55f) / 0.8f));
            float heavy = Mathf.Lerp(0.38f, 0.58f, Mathf.Clamp01((profile.LoadHandling - 0.55f) / 0.8f));
            return Mathf.Lerp(light, heavy, load);
        }

        static void TickFootingRecovery(WorkerLocomotionState loco, in WorkerPhysicalProfile profile, float dt)
        {
            if (loco == null || dt <= 0f) return;
            if (NowUnscaled >= loco.SlowUntilUnscaled)
            {
                loco.SlowMul = 1f;
                return;
            }
            float pull = 0.35f * profile.FootingRecovery * dt;
            loco.SlowMul = Mathf.Min(1f, loco.SlowMul + pull);
        }

        static void MaybeFootingEvent(
            WorkerRuntime wr,
            WorkerLocomotionState loco,
            in WorkerPhysicalProfile profile,
            in WorkerTerrainSample terrain,
            float carriedLoad01,
            float dt)
        {
            if (loco == null || wr?.Stats == null) return;

            // Exposure model: accumulate while moving on hard ground, then one discrete check
            float exposeRate = 0.55f + terrain.Difficulty01 * 1.1f;
            if (carriedLoad01 > 0.4f) exposeRate *= 1.15f;
            loco.ExposureAccum += exposeRate * dt;
            if (loco.ExposureAccum < ExposurePerCheck) return;

            float now = NowUnscaled;
            if (now < loco.NextCheckUnscaled) return;
            loco.NextCheckUnscaled = now + FootingCheckInterval;
            loco.ExposureAccum = 0f;

            float chance = 0.03f + terrain.Difficulty01 * 0.11f;
            chance *= Mathf.Lerp(1.3f, 0.65f, Mathf.Clamp01((profile.FootingResist - 0.4f) / 0.95f));

            var st = wr.State;
            if (st != null)
            {
                if (st.FocusState < 35f)
                    chance *= 1f + (35f - st.FocusState) / 90f;
                if (st.MentalFatigue > 70f)
                    chance *= 1f + (st.MentalFatigue - 70f) / 140f;
                if (st.StaminaPrimed)
                {
                    float max = wr.PhysicalStaminaMax;
                    if (max > 0.001f && st.PhysicalStamina / max <= 0.12f)
                        chance *= 1.35f;
                }
            }
            if (wr.Injuries != null && WorkerInjuryConsequences.RestrictsWalking(wr.Injuries))
                chance *= 1.25f;
            if (carriedLoad01 > 0.55f)
                chance *= 1.2f;

            // Competent rested worker usually succeeds on difficult terrain
            chance = Mathf.Clamp(chance, 0.015f, 0.28f);
            if (WorkerRoll.NextUnit() > chance) return;

            int dc = 8 + Mathf.RoundToInt(terrain.Difficulty01 * 10f);
            dc -= Mathf.RoundToInt((profile.StressStability - 0.9f) * 3f);
            dc = Mathf.Clamp(dc, 6, 18);

            int spatialMod = (wr.Stats.Get(WorkerStatId.SpatialGeometry) - 10) / 5;
            var roll = WorkerRoll.Check(wr.Stats, WorkerStatId.Balance, dc, spatialMod);
            if (roll.Success) return;

            int margin = dc - roll.Total;
            WorkerFootingEvent ev;
            float slowMul;
            float dur;
            if (margin >= 8 || (terrain.Kind == WorkerTerrainKind.Rubble && margin >= 4))
            {
                ev = WorkerFootingEvent.LossOfFooting;
                slowMul = 0.2f;
                dur = 0.7f;
            }
            else if (margin >= 3 || terrain.Difficulty01 >= 0.7f)
            {
                ev = WorkerFootingEvent.Stumble;
                slowMul = 0.45f;
                dur = 0.9f;
            }
            else
            {
                ev = WorkerFootingEvent.BriefSlow;
                slowMul = 0.65f;
                dur = 0.55f;
            }

            dur /= Mathf.Max(0.75f, profile.FootingRecovery);

            loco.LastEvent = ev;
            loco.LastEventUnscaled = now;
            loco.LastEventDetail =
                $"{ev} on {terrain.Kind} | Bal d20 {roll.D20}+{roll.StatValue}={roll.Total} vs DC {dc}";
            loco.SlowMul = Mathf.Min(loco.SlowMul, slowMul);
            loco.SlowUntilUnscaled = now + dur;

            var accident = WorkerAccidentSystem.ResolveFootingFail(
                wr, ev, terrain, carriedLoad01, margin);
            loco.LastAccident = accident;
            if (accident != null && accident.Outcome >= WorkerAccidentOutcome.Fell)
            {
                loco.SlowMul = Mathf.Min(loco.SlowMul, 0.15f);
                loco.SlowUntilUnscaled = now + Mathf.Max(dur, 1.1f);
            }
        }
    }
}
