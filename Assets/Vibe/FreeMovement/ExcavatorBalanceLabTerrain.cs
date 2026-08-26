using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Shared rock / bedrock / endurance lane carving for manual compare + automated benchmark.
    /// Terrain values only — no gameplay formula changes.
    /// </summary>
    public static class ExcavatorBalanceLabTerrain
    {
        public const int CellsAcross = 10;
        public const int RockLayers = 14;
        public const int BedrockLayers = 18;
        public const int FloorClear = 6;
        public const int Width = 16;

        public static int MaterialLayers(ExcavatorBalanceTestMode mode) => mode switch
        {
            ExcavatorBalanceTestMode.Rock => RockLayers,
            ExcavatorBalanceTestMode.Bedrock => BedrockLayers,
            _ => RockLayers + BedrockLayers,
        };

        public static int WorldHeight(ExcavatorBalanceTestMode mode) =>
            FloorClear + MaterialLayers(mode) + 8;

        public static void Carve(FineTerrainWorld w, ExcavatorBalanceTestMode mode)
        {
            w.BeginBatch();
            for (int y = 1; y < w.Height - 1; y++)
            for (int x = 1; x < w.Width - 1; x++)
            {
                if (y < FloorClear)
                {
                    w.InstantExcavate(x, y, notify: false);
                    continue;
                }

                int rockTop = FloorClear + RockLayers;
                int bedTop = rockTop + BedrockLayers;

                switch (mode)
                {
                    case ExcavatorBalanceTestMode.Rock:
                    {
                        int top = FloorClear + RockLayers;
                        if (y < top)
                            w.Set(x, y, FineTerrainWorld.MakeRock());
                        else
                            w.InstantExcavate(x, y, notify: false);
                        break;
                    }
                    case ExcavatorBalanceTestMode.Bedrock:
                    {
                        int top = FloorClear + BedrockLayers;
                        if (y < top)
                            w.Set(x, y, FineTerrainWorld.MakeHardRock(bedrockSockets: 3));
                        else
                            w.InstantExcavate(x, y, notify: false);
                        break;
                    }
                    default:
                    {
                        if (y < rockTop)
                            w.Set(x, y, FineTerrainWorld.MakeRock());
                        else if (y < bedTop)
                            w.Set(x, y, FineTerrainWorld.MakeHardRock(bedrockSockets: 3));
                        else
                            w.InstantExcavate(x, y, notify: false);
                        break;
                    }
                }
            }

            for (int x = 0; x < w.Width; x++)
            {
                w.Set(x, 0, FineTerrainWorld.MakeBedrock());
                w.Set(x, w.Height - 1, FineTerrainWorld.MakeBedrock());
            }
            for (int y = 0; y < w.Height; y++)
            {
                w.Set(0, y, FineTerrainWorld.MakeBedrock());
                w.Set(w.Width - 1, y, FineTerrainWorld.MakeBedrock());
            }
            w.EndBatch();
        }

        public static Vector2 StartLocal(FineTerrainWorld world)
        {
            int midX = world.Width / 2;
            int startY = FloorClear - 2;
            return world.CellCenter(midX, startY);
        }

        public static Vector2 GoalLocal(FineTerrainWorld world, ExcavatorBalanceTestMode mode)
        {
            int midX = world.Width / 2;
            int layers = MaterialLayers(mode);
            return world.CellCenter(midX, FloorClear + layers + 2);
        }
    }
}
