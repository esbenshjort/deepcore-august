using System;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Dynamic worker state that changes during play.
    /// Separate from permanent <see cref="WorkerStats"/> (1–20) and from machine state (e.g. Heat).
    /// Add new condition fields here as systems need them (Spooked, Injury, …).
    /// </summary>
    [Serializable]
    public sealed class WorkerConditions
    {
        public const float Min = 0f;
        public const float Max = 100f;

        [Header("CONDITIONS")]
        [Range(Min, Max)]
        [SerializeField] float frustration;

        [Range(Min, Max)]
        [SerializeField] float injury;

        [SerializeField] bool needsCare;

        [Header("STAMINA (runtime)")]
        [SerializeField] float currentStamina;
        [SerializeField] bool isResting;

        /// <summary>0–100. Tracking only until a future system consumes it.</summary>
        public float Frustration
        {
            get => frustration;
            set => frustration = Clamp(value);
        }

        /// <summary>0–100 physical Injury. Not a permanent WorkerStat; no auto-heal on rest yet.</summary>
        public float Injury
        {
            get => injury;
            set => injury = Clamp(value);
        }

        /// <summary>
        /// Set when Injury reaches Care threshold — Steward / Triage later.
        /// Does not heal by itself.
        /// </summary>
        public bool NeedsCare
        {
            get => needsCare;
            set => needsCare = value;
        }

        /// <summary>
        /// Runtime endurance pool. Max is derived from WorkerStats.Stamina by the excavator loop
        /// (MaxStamina = 50 + Stamina×5). Not a permanent WorkerStat.
        /// </summary>
        public float CurrentStamina
        {
            get => currentStamina;
            set => currentStamina = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Worker paused digging to recover Stamina. Mining assignment / route is kept.
        /// </summary>
        public bool IsResting
        {
            get => isResting;
            set => isResting = value;
        }

        public static float Clamp(float value) => Mathf.Clamp(value, Min, Max);

        public void ResetAll()
        {
            frustration = 0f;
            injury = 0f;
            needsCare = false;
            currentStamina = 0f;
            isResting = false;
        }

        /// <summary>Add Injury (clamped 0–100). Returns amount actually applied.</summary>
        public float AddInjury(float amount)
        {
            if (amount <= 0f) return 0f;
            float before = injury;
            Injury = injury + amount;
            return injury - before;
        }

        /// <summary>Clamp CurrentStamina into 0…max inclusive.</summary>
        public void SetStamina(float value, float max)
        {
            currentStamina = Mathf.Clamp(value, 0f, Mathf.Max(0f, max));
        }

        /// <summary>Add stamina (clamped to max). Returns amount actually gained.</summary>
        public float AddStamina(float amount, float max)
        {
            if (amount <= 0f || max <= 0f) return 0f;
            float before = currentStamina;
            SetStamina(currentStamina + amount, max);
            return currentStamina - before;
        }

        /// <summary>Spend stamina (floored at 0). Returns amount actually spent.</summary>
        public float SpendStamina(float amount)
        {
            if (amount <= 0f) return 0f;
            float before = currentStamina;
            currentStamina = Mathf.Max(0f, currentStamina - amount);
            return before - currentStamina;
        }

        /// <summary>Add to Frustration and return the clamped amount actually applied.</summary>
        public float AddFrustration(float amount)
        {
            float before = frustration;
            Frustration = frustration + amount;
            return Frustration - before;
        }

        /// <summary>Subtract from Frustration (floored at 0). Returns how much was actually removed.</summary>
        public float ReduceFrustration(float amount)
        {
            if (amount <= 0f) return 0f;
            float before = frustration;
            Frustration = frustration - amount;
            return before - frustration;
        }
    }
}
