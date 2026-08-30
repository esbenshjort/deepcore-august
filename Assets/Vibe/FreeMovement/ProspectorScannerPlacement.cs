using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Placement validation for heavy scanners — exact cell, no silent snap.
    /// </summary>
    public static class ProspectorScannerPlacement
    {
        public static bool TryValidate(
            FineTerrainWorld world,
            ProspectorPerson prospector,
            Vector2 worldPos,
            float bodyRadius,
            out Vector2 snappedCenter,
            out string failReason)
        {
            snappedCenter = worldPos;
            failReason = null;
            if (world == null)
            {
                failReason = "no world";
                return false;
            }

            var cell = world.WorldToCell(worldPos);
            if (!world.InBounds(cell.x, cell.y))
            {
                failReason = "out of bounds";
                return false;
            }

            if (!world.IsTunnelOpen(cell.x, cell.y))
            {
                failReason = "solid / not excavated tunnel";
                return false;
            }

            snappedCenter = world.CellCenter(cell.x, cell.y);
            if (world.CircleHitsSolid(snappedCenter, bodyRadius))
            {
                failReason = "blocked footprint";
                return false;
            }

            var nav = world.Navigation;
            if (nav != null)
            {
                if (!nav.IsBuilt) nav.Rebuild();
                int minC = Mathf.Max(1, PathAgentProfile.Worker(bodyRadius).MinClearance);
                if (!nav.CanAgentStand(cell.x, cell.y, minC))
                {
                    failReason = "insufficient clearance";
                    return false;
                }
            }

            if (prospector != null && !prospector.CanReachPoint(snappedCenter))
            {
                failReason = "unreachable from Prospector";
                return false;
            }

            return true;
        }
    }
}
