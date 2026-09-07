using System;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// V1.2A: mutable PERSON state — follows WorkerId, not job hosts.
    /// Separate from static <see cref="WorkerStats"/> and from machine/provider state (Heat, etc.).
    /// Plain data; no MonoBehaviour / Transform / host references.
    /// </summary>
    [Serializable]
    public class WorkerState
    {
        public const float MeterMin = 0f;
        public const float MeterMax = 100f;

        // ——— Defaults (neutral room to move; not perfect spawn) ———
        public const float DefaultMentalFatigue = 18f;
        public const float DefaultFocusState = 62f;
        public const float DefaultFrustration = 8f;
        public const float DefaultMorale = 55f;

        [Header("PHYSICAL")]
        [SerializeField] float physicalStamina;
        [SerializeField] bool isResting;
        [SerializeField] bool staminaPrimed;

        [Header("MENTAL / EMOTIONAL (0–100)")]
        [Range(MeterMin, MeterMax)] [SerializeField] float mentalFatigue = DefaultMentalFatigue;
        [Range(MeterMin, MeterMax)] [SerializeField] float focusState = DefaultFocusState;
        [Range(MeterMin, MeterMax)] [SerializeField] float frustration = DefaultFrustration;
        [Range(MeterMin, MeterMax)] [SerializeField] float morale = DefaultMorale;

        [Header("INJURY")]
        [Range(MeterMin, MeterMax)] [SerializeField] float injury;
        [SerializeField] bool needsCare;
        [SerializeField] bool incapacitated;
        [SerializeField] float incapacitatedGameHours = -1f;
        [SerializeField] string incapacitatedCause = "";
        [SerializeField] bool trappedFromCamp;

        [Header("VITALITY")]
        [SerializeField] bool isAlive = true;
        [SerializeField] float deathGameHours = -1f;
        [SerializeField] int killedByWorkerId;

        /// <summary>Care threshold — Steward / Triage later.</summary>
        public const float CareInjuryThreshold = 60f;
        /// <summary>Critical injury band before rare lethal outcomes.</summary>
        public const float CriticalInjuryThreshold = 75f;

        /// <summary>
        /// Current physical reserve. Max is derived by consumers from WorkerStats.Stamina
        /// (Excavator: Max = 50 + Stamina×5). Kept as absolute pool, not forced 0–100.
        /// </summary>
        public float PhysicalStamina
        {
            get => physicalStamina;
            set => physicalStamina = Mathf.Max(0f, value);
        }

        /// <summary>Compatibility alias for Excavator / legacy callers.</summary>
        public float CurrentStamina
        {
            get => PhysicalStamina;
            set => PhysicalStamina = value;
        }

        /// <summary>Paused digging to recover PhysicalStamina (assignment kept).</summary>
        public bool IsResting
        {
            get => isResting;
            set => isResting = value;
        }

        /// <summary>True after first stamina pool init for this person.</summary>
        public bool StaminaPrimed
        {
            get => staminaPrimed;
            set => staminaPrimed = value;
        }

        /// <summary>
        /// V1.2C: latched after PhysicalExhaustion threshold until recovery clears it.
        /// Prevents exhaustion event spam while remaining exhausted.
        /// </summary>
        [SerializeField] bool exhaustionLatched;

        public bool ExhaustionLatched
        {
            get => exhaustionLatched;
            set => exhaustionLatched = value;
        }

        /// <summary>Accumulated cognitive load. 0 = fresh, 100 = spent.</summary>
        public float MentalFatigue
        {
            get => mentalFatigue;
            set => mentalFatigue = ClampMeter(value);
        }

        /// <summary>Current concentration. Distinct from static WorkerStats.Focus.</summary>
        public float FocusState
        {
            get => focusState;
            set => focusState = ClampMeter(value);
        }

        /// <summary>Immediate pressure / irritation / unresolved setbacks.</summary>
        public float Frustration
        {
            get => frustration;
            set => frustration = ClampMeter(value);
        }

        /// <summary>Broader outlook / expedition spirit. Long timescale.</summary>
        public float Morale
        {
            get => morale;
            set => morale = ClampMeter(value);
        }

        /// <summary>Physical Injury 0–100. Not a WorkerStat.</summary>
        public float Injury
        {
            get => injury;
            set => injury = ClampMeter(value);
        }

        /// <summary>Set when Injury hits care threshold — Steward / Triage later.</summary>
        public bool NeedsCare
        {
            get => needsCare;
            set => needsCare = value;
        }

        /// <summary>
        /// Trauma prevents independent action — distinct from NeedsCare / Dead.
        /// Remains at physical location; never teleported.
        /// </summary>
        public bool Incapacitated => incapacitated;

        public float IncapacitatedGameHours => incapacitatedGameHours;
        public string IncapacitatedCause => incapacitatedCause ?? "";

        /// <summary>True when no open tunnel path from camp (collapse debris).</summary>
        public bool TrappedFromCamp
        {
            get => trappedFromCamp;
            set => trappedFromCamp = value;
        }

        /// <summary>False after a lethal social incident. Never auto-revives.</summary>
        public bool IsAlive => isAlive;

        /// <summary>Game-hours of death, or −1 if alive.</summary>
        public float DeathGameHours => deathGameHours;

        /// <summary>WorkerId of killer when known; 0 if unset.</summary>
        public int KilledByWorkerId => killedByWorkerId;

        public static float ClampMeter(float value) => Mathf.Clamp(value, MeterMin, MeterMax);

        /// <summary>Factory with conservative defaults (not perfect).</summary>
        public static WorkerState CreateDefault()
        {
            var s = new WorkerState();
            s.ResetToSpawnDefaults();
            return s;
        }

        public void ResetToSpawnDefaults()
        {
            physicalStamina = 0f;
            isResting = false;
            staminaPrimed = false;
            mentalFatigue = DefaultMentalFatigue;
            focusState = DefaultFocusState;
            frustration = DefaultFrustration;
            morale = DefaultMorale;
            injury = 0f;
            needsCare = false;
            exhaustionLatched = false;
            ClearIncapacitated();
            trappedFromCamp = false;
            // Do not revive — dead workers stay dead across spawn-default resets.
        }

        public void MarkIncapacitated(float gameHours, string cause)
        {
            if (!isAlive) return;
            incapacitated = true;
            incapacitatedGameHours = gameHours;
            incapacitatedCause = cause ?? "";
            needsCare = true;
            isResting = false;
        }

        public void ClearIncapacitated()
        {
            incapacitated = false;
            incapacitatedGameHours = -1f;
            incapacitatedCause = "";
        }

        /// <summary>Lethal social outcome. Persistent; no respawn.</summary>
        public void MarkDead(float gameHours, int killedBy = 0)
        {
            if (!isAlive) return;
            isAlive = false;
            deathGameHours = gameHours;
            killedByWorkerId = killedBy;
            needsCare = false;
            isResting = false;
            ClearIncapacitated();
            Injury = MeterMax;
        }

        /// <summary>DEV / audit only — never call from gameplay.</summary>
        public void DevRevive()
        {
            isAlive = true;
            deathGameHours = -1f;
            killedByWorkerId = 0;
            injury = 0f;
            needsCare = false;
            ClearIncapacitated();
            trappedFromCamp = false;
        }

        /// <summary>Balance harness / debug wipe of mutable meters (keeps defaults for new fields).</summary>
        public void ResetAll()
        {
            ResetToSpawnDefaults();
        }

        public float AddInjury(float amount)
        {
            if (!isAlive) return 0f;
            if (amount <= 0f) return 0f;
            float before = injury;
            Injury = injury + amount;
            if (injury >= CareInjuryThreshold)
                needsCare = true;
            return injury - before;
        }

        public void SetStamina(float value, float max) =>
            PhysicalStamina = Mathf.Clamp(value, 0f, Mathf.Max(0f, max));

        public float AddStamina(float amount, float max)
        {
            if (amount <= 0f || max <= 0f) return 0f;
            float before = PhysicalStamina;
            SetStamina(PhysicalStamina + amount, max);
            return PhysicalStamina - before;
        }

        public float SpendStamina(float amount)
        {
            if (amount <= 0f) return 0f;
            float before = PhysicalStamina;
            PhysicalStamina = Mathf.Max(0f, PhysicalStamina - amount);
            return before - PhysicalStamina;
        }

        public float AddFrustration(float amount)
        {
            float before = frustration;
            Frustration = frustration + amount;
            return Frustration - before;
        }

        public float ReduceFrustration(float amount)
        {
            if (amount <= 0f) return 0f;
            float before = frustration;
            Frustration = frustration - amount;
            return before - frustration;
        }

        /// <summary>
        /// Apply a fraction of one overnight recovery block (t=1 = full night coefficients).
        /// Does not wipe meters to perfect; Morale barely moves.
        /// </summary>
        public void ApplySleepRecoveryFraction(float nightFraction01, float maxPhysicalStamina)
        {
            float t = Mathf.Clamp01(nightFraction01);
            if (t <= 0f) return;

            IsResting = false;

            if (StaminaPrimed && maxPhysicalStamina > 0.001f)
            {
                float missing = Mathf.Max(0f, maxPhysicalStamina - PhysicalStamina);
                AddStamina(missing * WorkerSleepRecovery.PhysicalStaminaMissingRestore01 * t,
                    maxPhysicalStamina);
            }

            MentalFatigue = MentalFatigue - WorkerSleepRecovery.MentalFatigueRelief * t;

            FocusState = Mathf.Lerp(FocusState, WorkerSleepRecovery.FocusStateBaseline,
                WorkerSleepRecovery.FocusStateLerp * t);

            ReduceFrustration(EffectiveSleepFrustrationRelief(Frustration) * t);

            Morale = Mathf.Lerp(Morale, WorkerSleepRecovery.MoraleBaseline,
                WorkerSleepRecovery.MoraleLerp * t);
        }

        /// <summary>
        /// Sleep takes the edge off but does not wipe a rotten shift — high Frustration
        /// gets diminishing overnight relief.
        /// </summary>
        public static float EffectiveSleepFrustrationRelief(float currentFrustration)
        {
            float baseRelief = WorkerSleepRecovery.FrustrationRelief;
            if (currentFrustration <= 25f) return baseRelief;
            if (currentFrustration <= 50f) return baseRelief * 0.75f;
            if (currentFrustration <= 75f) return baseRelief * 0.5f;
            return baseRelief * 0.32f;
        }
    }

    /// <summary>Centralized overnight recovery coefficients (tune here).</summary>
    public static class WorkerSleepRecovery
    {
        /// <summary>Fraction of missing PhysicalStamina restored over a full night.</summary>
        public const float PhysicalStaminaMissingRestore01 = 0.90f;

        /// <summary>MentalFatigue points removed over a full night (not necessarily to 0).</summary>
        public const float MentalFatigueRelief = 40f;

        /// <summary>FocusState soft target after sleep.</summary>
        public const float FocusStateBaseline = 62f;

        /// <summary>How strongly FocusState pulls toward baseline over a full night.</summary>
        public const float FocusStateLerp = 0.50f;

        /// <summary>Frustration points removed over a full night (partial — residue allowed).</summary>
        public const float FrustrationRelief = 2.2f;

        /// <summary>Morale soft target — barely used.</summary>
        public const float MoraleBaseline = 55f;

        /// <summary>Tiny Morale normalization over a full night.</summary>
        public const float MoraleLerp = 0.06f;

        /// <summary>Game hours in a typical overnight (18:00 → 08:00).</summary>
        public const float TypicalNightGameHours = 14f;
    }
}
