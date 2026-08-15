namespace DeepCore.FreeMovement
{
    /// <summary>One excavated terrain cell kept intact until the washer splits its sockets.</summary>
    public struct OreCell
    {
        public byte Sockets;
        public float Mass;

        public SocketKind GetSocket(int index) =>
            (SocketKind)((Sockets >> (index * 2)) & 0b11);

        public int Count(SocketKind kind)
        {
            int n = 0;
            for (int i = 0; i < 4; i++)
                if (GetSocket(i) == kind) n++;
            return n;
        }

        public int GoldCount => Count(SocketKind.Gold);
        public int RockCount => Count(SocketKind.Rock);
        public int BedrockCount => Count(SocketKind.Bedrock);
        public bool IsGoldOre => GoldCount > 0;

        public static OreCell FromLoose(LoosePile pile)
        {
            if (pile == null) return default;
            byte sockets = pile.Sockets;
            if (sockets == 0 && pile.GoldGrade > 0)
            {
                // Reconstruct gold sockets if packed byte missing
                for (int i = 0; i < pile.GoldGrade && i < 4; i++)
                    sockets |= (byte)((int)SocketKind.Gold << (i * 2));
                for (int i = pile.GoldGrade; i < 4; i++)
                    sockets |= (byte)((int)SocketKind.Rock << (i * 2));
            }
            else if (sockets == 0)
            {
                for (int i = 0; i < 4; i++)
                    sockets |= (byte)((int)SocketKind.Rock << (i * 2));
            }
            return new OreCell
            {
                Sockets = sockets,
                Mass = pile.Mass,
            };
        }

        public static OreCell FromTerrain(TerrainCell cell) =>
            new() { Sockets = cell.Sockets, Mass = cell.Mass };
    }

    public enum RefinerPriority : byte { OreRock = 0, GoldOre = 1 }
}
