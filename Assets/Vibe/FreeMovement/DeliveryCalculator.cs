using System;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Hauler carry + delivered totals. Deposits split into rock/gold stockpiles.</summary>
    public sealed class DeliveryCalculator
    {
        public float RockMass;
        public int GoldValue;
        public int GoldSockets;
        public int Deliveries;
        public int PilesDelivered;

        public float CarryRockMass;
        public int CarryRockPiles;
        public float CarryGoldMass;
        public int CarryGoldValue;
        public int CarryGoldSockets;
        public int CarryGoldPiles;

        public int CarryPiles => CarryRockPiles + CarryGoldPiles;
        public float CarryRock => CarryRockMass + CarryGoldMass; // total mass in bag (UI)
        public Stockpile RockPile;
        public Stockpile GoldPile;

        public event Action Changed;

        public void BindStockpiles(Stockpile rock, Stockpile gold)
        {
            RockPile = rock;
            GoldPile = gold;
        }

        public void Reset()
        {
            RockMass = 0f;
            GoldValue = 0;
            GoldSockets = 0;
            Deliveries = 0;
            PilesDelivered = 0;
            ClearCarry();
            RockPile?.Reset();
            GoldPile?.Reset();
            Changed?.Invoke();
        }

        public void ClearCarry()
        {
            CarryRockMass = 0f;
            CarryRockPiles = 0;
            CarryGoldMass = 0f;
            CarryGoldValue = 0;
            CarryGoldSockets = 0;
            CarryGoldPiles = 0;
            Changed?.Invoke();
        }

        public void AddCarry(LoosePile pile)
        {
            if (pile == null) return;
            if (pile.IsGold)
            {
                CarryGoldMass += pile.Mass;
                CarryGoldSockets += pile.GoldGrade;
                CarryGoldValue += pile.GoldValue;
                CarryGoldPiles++;
            }
            else
            {
                CarryRockMass += pile.Mass;
                CarryRockPiles++;
            }
            Changed?.Invoke();
        }

        public void DepositCarry()
        {
            if (CarryPiles <= 0) return;

            if (CarryRockPiles > 0)
                RockPile?.DepositRock(CarryRockMass, CarryRockPiles);
            if (CarryGoldPiles > 0)
                GoldPile?.DepositGold(CarryGoldMass, CarryGoldSockets, CarryGoldValue, CarryGoldPiles);

            RockMass += CarryRockMass;
            // Keep top calculator in sync with gold stockpile totals
            GoldSockets += CarryGoldSockets;
            GoldValue += CarryGoldValue;
            // Also count rock mass that arrived with gold nuggets? Gold mass stays on gold pile only.
            PilesDelivered += CarryPiles;
            Deliveries++;
            ClearCarry();
        }
    }
}
