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
            if (CarryPiles <= 0 && _carryCells.Count == 0) return;

            if (_carryCells.Count > 0)
            {
                for (int i = 0; i < _carryCells.Count; i++)
                {
                    var c = _carryCells[i];
                    if (c.IsDiamondOre) DiamondPile?.EnqueueOreCell(c);
                    else if (c.IsGoldOre) GoldPile?.EnqueueOreCell(c);
                    else RockPile?.EnqueueOreCell(c);
                }
            }
            else
            {
                if (CarryRockPiles > 0)
                    RockPile?.DepositRock(CarryRockMass, CarryRockPiles);
                if (CarryGoldPiles > 0)
                    GoldPile?.DepositGold(CarryGoldMass, CarryGoldSockets, CarryGoldValue, CarryGoldPiles);
                if (CarryDiamondPiles > 0)
                    DiamondPile?.DepositDiamond(CarryDiamondMass, CarryDiamondSockets,
                        CarryDiamondValue, CarryDiamondPiles);
            }

            RockMass += CarryRockMass;
            GoldSockets += CarryGoldSockets;
            DiamondSockets += CarryDiamondSockets;
            PilesDelivered += Mathf.Max(CarryPiles, _carryCells.Count);
            Deliveries++;
            ClearCarry();
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
