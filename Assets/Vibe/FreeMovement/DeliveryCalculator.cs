using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>Hauler carry + delivered ore cells. Refiner washes cells into refined/dirt.</summary>
    public sealed class DeliveryCalculator
    {
        public float RockMass;
        public int GoldValue;
        public int GoldSockets;
        public int DiamondValue;
        public int DiamondSockets;
        public int RefinedGold;
        public int RefinedDiamond;
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
        public float CarryDiamondMass;
        public int CarryDiamondValue;
        public int CarryDiamondSockets;
        public int CarryDiamondPiles;

        readonly List<OreCell> _carryCells = new(16);

        public int CarryPiles => CarryRockPiles + CarryGoldPiles + CarryDiamondPiles;
        public int CarryCellCount => _carryCells.Count;
        public float CarryRock => CarryRockMass + CarryGoldMass + CarryDiamondMass;
        public Stockpile RockPile;
        public Stockpile GoldPile;
        public Stockpile DiamondPile;
        public Stockpile RefinedPile;
        public Stockpile RefinedDiamondPile;
        public Stockpile DirtPile;

        public event Action Changed;

        public void BindStockpiles(Stockpile rock, Stockpile gold,
            Stockpile refined = null, Stockpile dirt = null,
            Stockpile diamond = null, Stockpile refinedDiamond = null)
        {
            RockPile = rock;
            GoldPile = gold;
            DiamondPile = diamond;
            RefinedPile = refined;
            RefinedDiamondPile = refinedDiamond;
            DirtPile = dirt;
        }

        public void Reset()
        {
            RockMass = 0f;
            GoldValue = 0;
            GoldSockets = 0;
            DiamondValue = 0;
            DiamondSockets = 0;
            RefinedGold = 0;
            RefinedDiamond = 0;
            DirtPieces = 0;
            Deliveries = 0;
            PilesDelivered = 0;
            CellsWashed = 0;
            ClearCarry();
            RockPile?.Reset();
            GoldPile?.Reset();
            DiamondPile?.Reset();
            RefinedPile?.Reset();
            RefinedDiamondPile?.Reset();
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
            CarryDiamondMass = 0f;
            CarryDiamondValue = 0;
            CarryDiamondSockets = 0;
            CarryDiamondPiles = 0;
            _carryCells.Clear();
            Changed?.Invoke();
        }

        public void AddCarry(LoosePile pile)
        {
            if (pile == null) return;
            var cell = OreCell.FromLoose(pile);
            _carryCells.Add(cell);
            if (pile.IsDiamond)
            {
                CarryDiamondMass += pile.Mass;
                CarryDiamondSockets += pile.DiamondGrade;
                CarryDiamondValue += pile.DiamondValue;
                CarryDiamondPiles++;
            }
            else if (pile.IsGold)
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
            while (TryDepositOne()) { }
            if (CarryPiles > 0 || _carryCells.Count > 0)
            {
                // Fallback aggregate path (legacy totals without cells)
                if (_carryCells.Count == 0)
                {
                    if (CarryRockPiles > 0)
                        RockPile?.DepositRock(CarryRockMass, CarryRockPiles);
                    if (CarryGoldPiles > 0)
                        GoldPile?.DepositGold(CarryGoldMass, CarryGoldSockets, CarryGoldValue, CarryGoldPiles);
                    if (CarryDiamondPiles > 0)
                        DiamondPile?.DepositDiamond(CarryDiamondMass, CarryDiamondSockets,
                            CarryDiamondValue, CarryDiamondPiles);
                    RockMass += CarryRockMass;
                    GoldSockets += CarryGoldSockets;
                    DiamondSockets += CarryDiamondSockets;
                    PilesDelivered += CarryPiles;
                    Deliveries++;
                    ClearCarry();
                }
            }
        }

        /// <summary>Unload one carried cell into the matching stockpile. Returns false when cart empty.</summary>
        public bool TryDepositOne()
        {
            if (_carryCells.Count == 0) return false;
            int last = _carryCells.Count - 1;
            var c = _carryCells[last];
            _carryCells.RemoveAt(last);

            if (c.IsDiamondOre)
            {
                DiamondPile?.EnqueueOreCell(c);
                CarryDiamondPiles = Mathf.Max(0, CarryDiamondPiles - 1);
                CarryDiamondMass = Mathf.Max(0f, CarryDiamondMass - c.Mass);
                CarryDiamondSockets = Mathf.Max(0, CarryDiamondSockets - c.DiamondCount);
                int dVal = c.DiamondCount <= 0 ? 0 : c.DiamondCount * (c.DiamondCount + 5) / 2;
                CarryDiamondValue = Mathf.Max(0, CarryDiamondValue - dVal);
                DiamondSockets += c.DiamondCount;
            }
            else if (c.IsGoldOre)
            {
                GoldPile?.EnqueueOreCell(c);
                CarryGoldPiles = Mathf.Max(0, CarryGoldPiles - 1);
                CarryGoldMass = Mathf.Max(0f, CarryGoldMass - c.Mass);
                CarryGoldSockets = Mathf.Max(0, CarryGoldSockets - c.GoldCount);
                int gVal = c.GoldCount <= 0 ? 0 : c.GoldCount * (c.GoldCount + 1) / 2;
                CarryGoldValue = Mathf.Max(0, CarryGoldValue - gVal);
                GoldSockets += c.GoldCount;
            }
            else
            {
                RockPile?.EnqueueOreCell(c);
                CarryRockPiles = Mathf.Max(0, CarryRockPiles - 1);
                CarryRockMass = Mathf.Max(0f, CarryRockMass - c.Mass);
                RockMass += c.Mass;
            }

            PilesDelivered++;
            if (_carryCells.Count == 0)
                Deliveries++;
            Changed?.Invoke();
            return true;
        }

        public void NotifyWashResult(int goldPieces, int dirtPieces, int diamondPieces = 0)
        {
            RefinedGold += Mathf.Max(0, goldPieces);
            RefinedDiamond += Mathf.Max(0, diamondPieces);
            DirtPieces += Mathf.Max(0, dirtPieces);
            GoldValue = RefinedGold;
            DiamondValue = RefinedDiamond;
            CellsWashed++;
            Changed?.Invoke();
        }
    }
}
