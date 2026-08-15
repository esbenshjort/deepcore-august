using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Hauler carry + delivered ore cells. Refiner washes cells into refined/dirt.</summary>
    public sealed class DeliveryCalculator
    {
        public float RockMass;
        public int GoldValue;       // refined gold pieces (post-washer)
        public int GoldSockets;     // ore gold sockets delivered (pre-wash)
        public int RefinedGold;
        public int DirtPieces;
        public int Deliveries;
        public int PilesDelivered;
        public int CellsWashed;

        public float CarryRockMass;
        public int CarryRockPiles;
        public float CarryGoldMass;
        public int CarryGoldValue;
        public int CarryGoldSockets;
        public int CarryGoldPiles;

        readonly List<OreCell> _carryCells = new(16);

        public int CarryPiles => CarryRockPiles + CarryGoldPiles;
        public float CarryRock => CarryRockMass + CarryGoldMass;
        public Stockpile RockPile;
        public Stockpile GoldPile;
        public Stockpile RefinedPile;
        public Stockpile DirtPile;

        public event Action Changed;

        public void BindStockpiles(Stockpile rock, Stockpile gold,
            Stockpile refined = null, Stockpile dirt = null)
        {
            RockPile = rock;
            GoldPile = gold;
            RefinedPile = refined;
            DirtPile = dirt;
        }

        public void Reset()
        {
            RockMass = 0f;
            GoldValue = 0;
            GoldSockets = 0;
            RefinedGold = 0;
            DirtPieces = 0;
            Deliveries = 0;
            PilesDelivered = 0;
            CellsWashed = 0;
            ClearCarry();
            RockPile?.Reset();
            GoldPile?.Reset();
            RefinedPile?.Reset();
            DirtPile?.Reset();
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
            _carryCells.Clear();
            Changed?.Invoke();
        }

        public void AddCarry(LoosePile pile)
        {
            if (pile == null) return;
            var cell = OreCell.FromLoose(pile);
            _carryCells.Add(cell);
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
            if (CarryPiles <= 0 && _carryCells.Count == 0) return;

            // Prefer intact cell queue — each cell stays whole until washer
            if (_carryCells.Count > 0)
            {
                for (int i = 0; i < _carryCells.Count; i++)
                {
                    var c = _carryCells[i];
                    if (c.IsGoldOre) GoldPile?.EnqueueOreCell(c);
                    else RockPile?.EnqueueOreCell(c);
                }
            }
            else
            {
                if (CarryRockPiles > 0)
                    RockPile?.DepositRock(CarryRockMass, CarryRockPiles);
                if (CarryGoldPiles > 0)
                    GoldPile?.DepositGold(CarryGoldMass, CarryGoldSockets, CarryGoldValue, CarryGoldPiles);
            }

            RockMass += CarryRockMass;
            GoldSockets += CarryGoldSockets;
            PilesDelivered += Mathf.Max(CarryPiles, _carryCells.Count);
            Deliveries++;
            ClearCarry();
        }

        public void NotifyWashResult(int goldPieces, int dirtPieces)
        {
            RefinedGold += Mathf.Max(0, goldPieces);
            DirtPieces += Mathf.Max(0, dirtPieces);
            GoldValue = RefinedGold; // HUD "GOLD" reads refined output
            CellsWashed++;
            Changed?.Invoke();
        }
    }
}
