using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Engineer infrastructure: sparse lanterns + simple tunnel supports.
    /// Track/road building is disabled.
    /// </summary>
    public sealed class MineInfrastructure
    {
        public float TrackSpeedMul = 1.25f;
        public float LanternInstallGameHours = 0.2f;
        public float SupportBuildGameHours = 0.28f;

        public float LanternOuterRadius = 3.15f;
        /// <summary>Dark gaps remain, but no long unlit active stretches.</summary>
        public float LanternMinSpacingWorld = 3.85f;
        public float LanternDarkMaxIllum = 0.34f;
        public float LanternInstallIntensity = 2.05f;
        public float LanternDigFaceClearance = 1.2f;
        public float MinLanternScore = 1.8f;

        public float SupportRadiusWorld = 0.95f;
        public float SupportCriticalScore = 3.8f;
        public float SupportPreventScore = 1.5f;
        /// <summary>Base along-tunnel spacing between support pairs (tighter when wide).</summary>
        public float SupportMinSpacingWorld = 1.15f;
        public float SupportWideSpacingWorld = 0.72f;
        public int MaxProposedDebug = 48;
        public int MaxCorridorCells = 5000;

        readonly FineTerrainWorld _world;
        readonly LogisticsTrafficMap _traffic;
        readonly List<Vector2Int> _lanternCells = new(32);
        readonly List<Vector2> _lanternWorld = new(32);
        readonly List<Vector2Int> _supportCells = new(48);
        readonly List<Vector2> _supportWorld = new(48);
        readonly List<float> _supportQuality = new(48);
        readonly List<float> _supportCondition = new(48);
        readonly Dictionary<long, float> _excavateHours = new(512);
        readonly List<Vector2Int> _proposedScratch = new(64);
        readonly List<Vector2Int> _corridor = new(1024);
        readonly Queue<Vector2Int> _bfsQ = new(1024);
        readonly HashSet<long> _bfsSeen = new();
        readonly int _w;
        readonly int _h;

        Transform _lanternRoot;
        Transform _supportRoot;
        Sprite _pillarSprite;
        Sprite _pillarBulbSprite;
        FreeWorkerController _excavator;
        Vector2 _dropWorld;
        bool _hasDrop;
        float _gameHours;
        Vector2Int _pendingPillarL;
        Vector2Int _pendingPillarR;
        bool _hasPendingPair;

        public LogisticsTrafficMap Traffic => _traffic;
        public int TrackCellCount => 0;
        public int LanternCount => _lanternCells.Count;
        public int SupportCount => _supportCells.Count;
        public IReadOnlyList<Vector2Int> ProposedDebug => _proposedScratch;
        public float TrackBuildGameHours => 0f;

        /// <summary>Last lantern evaluation dump (for Engineer HUD / log).</summary>
        public string LastLanternEvalDebug { get; private set; } = "lantern: not evaluated";
        /// <summary>Last support evaluation dump.</summary>
        public string LastSupportEvalDebug { get; private set; } = "support: not evaluated";

        public MineInfrastructure(
            FineTerrainWorld world,
            LogisticsTrafficMap traffic,
            Transform trackRoot,
            Transform lanternRoot)
        {
            _ = trackRoot;
            _world = world;
            _traffic = traffic;
            _lanternRoot = lanternRoot;
            _w = world.Width;
            _h = world.Height;
            _supportRoot = new GameObject("Supports").transform;
            if (lanternRoot != null && lanternRoot.parent != null)
                _supportRoot.SetParent(lanternRoot.parent, false);
            _pillarSprite = MakePillarSprite();
            _pillarBulbSprite = MakePillarBulbSprite();
            _world.RegionChanged += OnRegionChanged;
        }

        public void BindExcavator(FreeWorkerController excavator) => _excavator = excavator;

        public void BindDropPoint(Vector2 dropWorld)
        {
            _dropWorld = dropWorld;
            _hasDrop = true;
        }

        public void SetGameHours(float absoluteGameHours) => _gameHours = absoluteGameHours;

        public float TrackSpeedMulAt(Vector2 worldPos)
        {
            _ = worldPos;
            return 1f;
        }

        public bool HasTrack(int x, int y) => false;

        public void RefreshProposedDebug()
        {
            _proposedScratch.Clear();
            if (_excavator == null) return;
            Vector2 from = _excavator.Position;
            if (TryPickNextLanternCell(from, out var lan, out _))
                _proposedScratch.Add(lan);
            if (TryPickNextSupportCell(from, criticalOnly: false, out var sup, out _))
                _proposedScratch.Add(sup);
        }

        // ——— Lanterns ———

        public bool CanPlaceLantern(Vector2 worldPos)
        {
            if (_world == null) return false;
            var c = _world.WorldToCell(worldPos);
            if (!_world.IsTunnelOpen(c.x, c.y) && !_world.IsExcavated(c.x, c.y)) return false;
            float min2 = LanternMinSpacingWorld * LanternMinSpacingWorld;
            for (int i = 0; i < _lanternWorld.Count; i++)
                if ((_lanternWorld[i] - worldPos).sqrMagnitude < min2)
                    return false;
            return true;
        }

        public float EvaluateIllumination01(Vector2 worldPos)
        {
            float outer = Mathf.Max(0.5f, LanternOuterRadius);
            float best = 0f;
            for (int i = 0; i < _lanternWorld.Count; i++)
            {
                float d = Vector2.Distance(_lanternWorld[i], worldPos);
                if (d >= outer) continue;
                float t = 1f - d / outer;
                float contrib = t * t * (0.55f + 0.45f * t);
                if (contrib > best) best = contrib;
            }
            return best;
        }

        public float NearestLanternDist(Vector2 worldPos)
        {
            if (_lanternWorld.Count == 0) return 999f;
            float best = float.MaxValue;
            for (int i = 0; i < _lanternWorld.Count; i++)
            {
                float d = Vector2.Distance(_lanternWorld[i], worldPos);
                if (d < best) best = d;
            }
            return best;
        }

        public float NearestSupportDist(Vector2 worldPos)
        {
            if (_supportWorld.Count == 0) return 999f;
            float best = float.MaxValue;
            for (int i = 0; i < _supportWorld.Count; i++)
            {
                float d = Vector2.Distance(_supportWorld[i], worldPos);
                if (d < best) best = d;
            }
            return best;
        }

        public bool IsGenuinelyDark(Vector2 worldPos) =>
            EvaluateIllumination01(worldPos) <= LanternDarkMaxIllum;

        public bool TryPlaceLantern(Vector2 worldPos, float intensity = -1f)
        {
            if (_world == null) return false;
            var c = _world.WorldToCell(worldPos);
            c = PreferTunnelSideCell(c);
            Vector2 center = _world.CellCenter(c.x, c.y);
            if (!CanPlaceLantern(center)) return false;
            if (intensity < 0f) intensity = LanternInstallIntensity;
            DigVisualKit.PlaceLantern(_lanternRoot, center, local: true, intensity: intensity);
            RegisterLantern(center);
            return true;
        }

        public void RegisterLantern(Vector2 worldPos)
        {
            if (_world == null) return;
            var c = _world.WorldToCell(worldPos);
            Vector2 center = _world.CellCenter(c.x, c.y);
            _lanternCells.Add(c);
            _lanternWorld.Add(center);
        }

        /// <summary>
        /// Dark cell on the connected excavated corridor between base and excavator.
        /// Prefers sites off the dig face with sparse spacing.
        /// </summary>
        public bool TryPickNextLanternCell(Vector2 engineerWorld, out Vector2Int cell, out float score)
        {
            cell = default;
            score = 0f;
            var sb = new StringBuilder(256);

            if (_world == null || _excavator == null)
            {
                LastLanternEvalDebug = "LANTERN | reject: no world/excavator";
                return false;
            }

            CollectCorridorBetweenBaseAndDig(_corridor);
            sb.Append($"LANTERN | corridorCells={_corridor.Count}");

            if (_corridor.Count == 0)
            {
                sb.Append(" | reject: empty corridor (base↔dig not connected?)");
                LastLanternEvalDebug = sb.ToString();
                return false;
            }

            Vector2 dig = _excavator.Position;
            Vector2 facing = _excavator.Facing.sqrMagnitude > 0.0001f
                ? _excavator.Facing.normalized
                : Vector2.up;

            // One connectivity check: same open component ⇒ all corridor cells reachable
            bool corridorPathFromEng = HasTunnelPath(engineerWorld, dig);
            sb.Append($" | engPathToDig={corridorPathFromEng}");

            // Track darkest relevant cell even if rejected
            float darkestIllum = 999f;
            Vector2Int darkestCell = default;
            float darkestLampDist = 0f;
            bool darkestPath = corridorPathFromEng;
            string darkestReject = "none scanned";

            float best = 0f;
            Vector2Int bestCell = default;
            string bestNote = "";

            int stride = _corridor.Count > 800 ? 2 : 1;

            for (int i = 0; i < _corridor.Count; i += stride)
            {
                var c = _corridor[i];
                if (!_world.IsTunnelOpen(c.x, c.y)) continue;
                Vector2 world = _world.CellCenter(c.x, c.y);
                float illum = EvaluateIllumination01(world);
                float lampDist = NearestLanternDist(world);
                float distDig = Vector2.Distance(world, dig);
                bool pathOk = corridorPathFromEng;

                string reject = null;
                if (distDig < LanternDigFaceClearance)
                    reject = $"too close to dig face ({distDig:0.00}<{LanternDigFaceClearance:0.00})";
                else if (!CanPlaceLantern(world))
                    reject = $"spacing (nearestLamp={lampDist:0.00}, need>={LanternMinSpacingWorld:0.00})";
                else if (illum > LanternDarkMaxIllum)
                    reject = $"not dark enough (light={illum:0.00}>{LanternDarkMaxIllum:0.00})";
                else if (!pathOk)
                    reject = "no tunnel path from engineer";

                float ahead = Vector2.Dot(world - dig, facing);

                if (illum < darkestIllum)
                {
                    darkestIllum = illum;
                    darkestCell = c;
                    darkestLampDist = lampDist;
                    darkestPath = pathOk;
                    darkestReject = reject ?? "ok candidate";
                }

                if (reject != null) continue;

                // Prefer wall-side cells over travel centerline
                var side = PreferTunnelSideCell(c);
                Vector2 sideWorld = _world.CellCenter(side.x, side.y);
                if (!CanPlaceLantern(sideWorld) || EvaluateIllumination01(sideWorld) > LanternDarkMaxIllum)
                    side = c;
                else
                {
                    c = side;
                    world = sideWorld;
                    illum = EvaluateIllumination01(world);
                    lampDist = NearestLanternDist(world);
                }

                float darkness = LanternDarkMaxIllum - illum;
                float s = 3f + darkness * 12f;
                // 1) active tunnel toward excavator
                s += Mathf.Clamp01(1f - distDig / 7f) * 4f;
                // 2) newly excavated
                if (_excavateHours.TryGetValue(Key(c.x, c.y), out float t0) && _gameHours - t0 < 10f)
                    s += 2.8f;
                // 3) logistics / traffic
                if (_traffic != null)
                    s += Mathf.Min(2.2f, _traffic.Intensity01(c.x, c.y) * 2.5f);
                if (ahead < 0f) s += 1.6f;
                else if (ahead < 0.5f) s += 0.8f;
                if (IsAdjacentToSolid(c.x, c.y)) s += 2.2f; // side of tunnel
                if (IsLikelyCenterline(c.x, c.y)) s -= 1.8f;
                if (pathOk) s += 0.5f;

                if (s > best)
                {
                    best = s;
                    bestCell = c;
                    bestNote = $"light={illum:0.00} lampDist={lampDist:0.00} digDist={distDig:0.00} path={pathOk}";
                }
            }

            sb.Append($" | darkest=({darkestCell.x},{darkestCell.y}) light={darkestIllum:0.00} nearestLamp={darkestLampDist:0.00} path={darkestPath} note={darkestReject}");

            if (best < MinLanternScore)
            {
                sb.Append($" | reject: no valid site (bestScore={best:0.00}<{MinLanternScore:0.00})");
                LastLanternEvalDebug = sb.ToString();
                return false;
            }

            cell = bestCell;
            score = best;
            sb.Append($" | SELECT ({bestCell.x},{bestCell.y}) score={best:0.00} {bestNote}");
            LastLanternEvalDebug = sb.ToString();
            _proposedScratch.Clear();
            _proposedScratch.Add(bestCell);
            return true;
        }

        // ——— Tunnel supports (paired wall pillars) ———

        public float EvaluateInstability(int x, int y)
        {
            if (!InBounds(x, y) || !_world.IsTunnelOpen(x, y)) return 0f;
            float width = LocalWidthScore(x, y);
            if (width < 0.55f) return 0f;

            float age = 1.0f;
            long key = Key(x, y);
            if (_excavateHours.TryGetValue(key, out float t0))
                age = Mathf.Max(0.4f, _gameHours - t0 + 1.0f);

            float support = NearbySupportRelief(x, y);
            return width * (1.25f + age * 0.22f) - support;
        }

        float RequiredSupportSpacing(float widthScore) =>
            Mathf.Lerp(SupportMinSpacingWorld, SupportWideSpacingWorld, Mathf.Clamp01(widthScore / 6f));

        public bool TryPickNextSupportCell(Vector2 engineerWorld, bool criticalOnly, out Vector2Int cell, out float score)
        {
            cell = default;
            score = 0f;
            _hasPendingPair = false;
            var sb = new StringBuilder(256);

            if (_world == null || _excavator == null)
            {
                LastSupportEvalDebug = "SUPPORT | reject: no world/excavator";
                return false;
            }

            CollectCorridorBetweenBaseAndDig(_corridor);
            float gate = criticalOnly ? SupportCriticalScore : SupportPreventScore;
            sb.Append($"SUPPORT | {(criticalOnly ? "critical" : "prevent")} gate={gate:0.00} corridor={_corridor.Count}");

            if (_corridor.Count == 0)
            {
                sb.Append(" | reject: empty corridor");
                LastSupportEvalDebug = sb.ToString();
                return false;
            }

            Vector2 dig = _excavator.Position;
            bool corridorPathFromEng = HasTunnelPath(engineerWorld, dig);
            sb.Append($" | engPathToDig={corridorPathFromEng}");

            float bestInst = -1f;
            Vector2Int bestInstCell = default;
            float bestInstSupDist = 0f;
            bool bestInstPath = corridorPathFromEng;
            string bestInstReject = "none";

            float best = 0f;
            Vector2Int bestCell = default;
            Vector2Int bestL = default, bestR = default;
            string bestNote = "";

            int stride = _corridor.Count > 800 ? 2 : 1;
            for (int i = 0; i < _corridor.Count; i += stride)
            {
                var c = _corridor[i];
                if (!_world.IsTunnelOpen(c.x, c.y)) continue;
                if (!IsLikelyCenterline(c.x, c.y) && LocalWidthScore(c.x, c.y) < 2.2f)
                    continue;

                Vector2 world = _world.CellCenter(c.x, c.y);
                float width = LocalWidthScore(c.x, c.y);
                float needGap = RequiredSupportSpacing(width);
                float supDist = NearestSupportDist(world);
                float inst = EvaluateInstability(c.x, c.y);
                bool pathOk = corridorPathFromEng;

                string reject = null;
                if (!TryResolvePillarPair(c, out var left, out var right))
                    reject = "no wall pillar pair";
                else if (supDist < needGap)
                    reject = $"spacing (nearest={supDist:0.00}<need {needGap:0.00})";
                else if (!pathOk)
                    reject = "no tunnel path from engineer";
                else if (criticalOnly && inst < gate)
                    reject = $"below critical (inst={inst:0.00})";
                // Preventative: regular interval along tunnel is enough (gap already checked)

                if (inst > bestInst)
                {
                    bestInst = inst;
                    bestInstCell = c;
                    bestInstSupDist = supDist;
                    bestInstPath = pathOk;
                    bestInstReject = reject ?? "ok candidate";
                }

                if (reject != null) continue;

                float s = Mathf.Max(inst, 1.2f);
                s += (supDist / Mathf.Max(0.01f, needGap)) * 2.4f;
                s += Mathf.Clamp01(1f - Vector2.Distance(world, dig) / 8f) * 1.6f;
                if (_excavateHours.TryGetValue(Key(c.x, c.y), out float t0) && _gameHours - t0 < 12f)
                    s += 2.2f;
                if (pathOk) s += 0.25f;

                if (s > best)
                {
                    best = s;
                    bestCell = c;
                    bestL = left;
                    bestR = right;
                    bestNote = $"inst={inst:0.00} nearestSupport={supDist:0.00} needGap={needGap:0.00} path={pathOk}";
                }
            }

            sb.Append($" | highest=({bestInstCell.x},{bestInstCell.y}) inst={bestInst:0.00} nearestSupport={bestInstSupDist:0.00} path={bestInstPath} note={bestInstReject}");

            if (best < 0.01f || (bestL.x == bestR.x && bestL.y == bestR.y))
            {
                sb.Append($" | reject: no valid pair site (best={best:0.00})");
                LastSupportEvalDebug = sb.ToString();
                return false;
            }

            if (criticalOnly && bestInst < gate)
            {
                sb.Append(" | reject: nothing critical");
                LastSupportEvalDebug = sb.ToString();
                return false;
            }

            cell = bestCell;
            score = best;
            _pendingPillarL = bestL;
            _pendingPillarR = bestR;
            _hasPendingPair = true;
            sb.Append($" | SELECT ({bestCell.x},{bestCell.y}) pillars L({bestL.x},{bestL.y}) R({bestR.x},{bestR.y}) {bestNote}");
            LastSupportEvalDebug = sb.ToString();
            return true;
        }

        public bool CanBuildSupport(int x, int y)
        {
            if (!InBounds(x, y) || !_world.IsTunnelOpen(x, y)) return false;
            Vector2 world = _world.CellCenter(x, y);
            float width = LocalWidthScore(x, y);
            float need = RequiredSupportSpacing(width);
            return NearestSupportDist(world) >= need * 0.9f;
        }

        public bool TryBuildSupport(int x, int y, float quality01 = 0.8f)
        {
            Vector2Int left, right;
            if (_hasPendingPair)
            {
                left = _pendingPillarL;
                right = _pendingPillarR;
                _hasPendingPair = false;
            }
            else if (!TryResolvePillarPair(new Vector2Int(x, y), out left, out right))
                return false;

            if (!_world.IsTunnelOpen(left.x, left.y) || !_world.IsTunnelOpen(right.x, right.y))
                return false;

            Vector2 mid = (_world.CellCenter(left.x, left.y) + _world.CellCenter(right.x, right.y)) * 0.5f;
            _supportCells.Add(new Vector2Int(x, y));
            _supportWorld.Add(mid);
            _supportQuality.Add(Mathf.Clamp(quality01, 0.35f, 1.15f));
            _supportCondition.Add(1f);
            SpawnPillarVisual(left.x, left.y);
            SpawnPillarVisual(right.x, right.y);
            return true;
        }

        /// <summary>Quality of nearest support (0 if none). Used by collapse stability.</summary>
        public float NearestSupportQuality01(Vector2 worldPos)
        {
            if (_supportWorld.Count == 0) return 0f;
            float best = float.MaxValue;
            int bestI = -1;
            for (int i = 0; i < _supportWorld.Count; i++)
            {
                float d = Vector2.Distance(_supportWorld[i], worldPos);
                if (d < best) { best = d; bestI = i; }
            }
            if (bestI < 0 || bestI >= _supportQuality.Count) return 0.75f;
            float cond = bestI < _supportCondition.Count ? _supportCondition[bestI] : 1f;
            return Mathf.Clamp01(_supportQuality[bestI] * cond);
        }

        public void DamageSupportConditionNear(Vector2 epicenter, float amount)
        {
            if (amount <= 0f) return;
            float r2 = (SupportRadiusWorld * 2.2f) * (SupportRadiusWorld * 2.2f);
            for (int i = 0; i < _supportWorld.Count; i++)
            {
                if ((_supportWorld[i] - epicenter).sqrMagnitude > r2) continue;
                if (i >= _supportCondition.Count) continue;
                _supportCondition[i] = Mathf.Clamp(_supportCondition[i] - amount, 0.25f, 1f);
            }
        }

        bool TryResolvePillarPair(Vector2Int station, out Vector2Int left, out Vector2Int right)
        {
            left = right = station;
            if (!InBounds(station.x, station.y) || !_world.IsTunnelOpen(station.x, station.y))
                return false;

            bool preferHoriz = CountOpenAlong(station.x, station.y, 1, 0) + CountOpenAlong(station.x, station.y, -1, 0)
                <= CountOpenAlong(station.x, station.y, 0, 1) + CountOpenAlong(station.x, station.y, 0, -1);

            Vector2Int n0 = preferHoriz ? new Vector2Int(-1, 0) : new Vector2Int(0, -1);
            Vector2Int n1 = preferHoriz ? new Vector2Int(1, 0) : new Vector2Int(0, 1);

            if (!TryWalkToWallPillar(station, n0, out left)) return false;
            if (!TryWalkToWallPillar(station, n1, out right)) return false;
            if (left == right) return false;
            if (left == station || right == station) return false;
            return true;
        }

        bool TryWalkToWallPillar(Vector2Int start, Vector2Int dir, out Vector2Int pillar)
        {
            pillar = start;
            int x = start.x, y = start.y;
            Vector2Int lastOpen = start;
            for (int step = 0; step < 14; step++)
            {
                x += dir.x;
                y += dir.y;
                if (!InBounds(x, y) || !_world.IsTunnelOpen(x, y))
                {
                    pillar = lastOpen;
                    return lastOpen != start && IsAdjacentToSolid(lastOpen.x, lastOpen.y);
                }
                lastOpen = new Vector2Int(x, y);
            }
            pillar = lastOpen;
            return lastOpen != start && IsAdjacentToSolid(lastOpen.x, lastOpen.y);
        }

        int CountOpenAlong(int x, int y, int dx, int dy)
        {
            int n = 0;
            for (int i = 1; i <= 8; i++)
            {
                int nx = x + dx * i, ny = y + dy * i;
                if (!InBounds(nx, ny) || !_world.IsTunnelOpen(nx, ny)) break;
                n++;
            }
            return n;
        }

        Vector2Int PreferTunnelSideCell(Vector2Int c)
        {
            if (!InBounds(c.x, c.y)) return c;
            if (IsAdjacentToSolid(c.x, c.y) && _world.IsTunnelOpen(c.x, c.y))
                return c;

            Vector2Int best = c;
            int bestScore = -1;
            for (int oy = -2; oy <= 2; oy++)
            for (int ox = -2; ox <= 2; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int nx = c.x + ox, ny = c.y + oy;
                if (!InBounds(nx, ny) || !_world.IsTunnelOpen(nx, ny)) continue;
                if (!IsAdjacentToSolid(nx, ny)) continue;
                int s = 3 - (Mathf.Abs(ox) + Mathf.Abs(oy));
                if (IsLikelyCenterline(nx, ny)) s -= 2;
                if (s > bestScore)
                {
                    bestScore = s;
                    best = new Vector2Int(nx, ny);
                }
            }
            return bestScore >= 0 ? best : c;
        }

        bool IsAdjacentToSolid(int x, int y) =>
            IsSolidCell(x + 1, y) || IsSolidCell(x - 1, y)
            || IsSolidCell(x, y + 1) || IsSolidCell(x, y - 1);

        bool IsSolidCell(int x, int y) =>
            !InBounds(x, y) || !_world.IsTunnelOpen(x, y);

        bool IsLikelyCenterline(int x, int y)
        {
            bool h = _world.IsTunnelOpen(x - 1, y) && _world.IsTunnelOpen(x + 1, y);
            bool v = _world.IsTunnelOpen(x, y - 1) && _world.IsTunnelOpen(x, y + 1);
            int solidN = 0;
            if (IsSolidCell(x + 1, y)) solidN++;
            if (IsSolidCell(x - 1, y)) solidN++;
            if (IsSolidCell(x, y + 1)) solidN++;
            if (IsSolidCell(x, y - 1)) solidN++;
            return solidN == 0 && (h || v);
        }

        // ——— Corridor / path ———

        /// <summary>
        /// Connected excavated component containing the excavator.
        /// If base/drop is in that component, the full mining network is searched.
        /// </summary>
        void CollectCorridorBetweenBaseAndDig(List<Vector2Int> dst)
        {
            dst.Clear();
            if (_world == null || _excavator == null) return;

            Vector2Int dig = SnapOpen(_world.WorldToCell(_excavator.Position));
            if (!InBounds(dig.x, dig.y) || !_world.IsTunnelOpen(dig.x, dig.y)) return;

            _bfsSeen.Clear();
            _bfsQ.Clear();
            _bfsQ.Enqueue(dig);
            _bfsSeen.Add(Key(dig.x, dig.y));

            bool reachedDrop = false;
            Vector2Int drop = default;
            if (_hasDrop)
            {
                drop = SnapOpen(_world.WorldToCell(_dropWorld));
                if (drop == dig) reachedDrop = true;
            }

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            while (_bfsQ.Count > 0 && dst.Count < MaxCorridorCells)
            {
                var cur = _bfsQ.Dequeue();
                dst.Add(cur);
                if (_hasDrop && cur == drop) reachedDrop = true;

                for (int k = 0; k < 4; k++)
                {
                    int nx = cur.x + dx[k], ny = cur.y + dy[k];
                    if (!InBounds(nx, ny) || !_world.IsTunnelOpen(nx, ny)) continue;
                    long key = Key(nx, ny);
                    if (!_bfsSeen.Add(key)) continue;
                    _bfsQ.Enqueue(new Vector2Int(nx, ny));
                }
            }

            // If drop wasn't reached, still keep dig-connected excavated area (active mine).
            _ = reachedDrop;
        }

        bool HasTunnelPath(Vector2 fromWorld, Vector2 toWorld)
        {
            if (_world == null) return false;
            var a = SnapOpen(_world.WorldToCell(fromWorld));
            var b = SnapOpen(_world.WorldToCell(toWorld));
            if (!InBounds(a.x, a.y) || !InBounds(b.x, b.y)) return false;
            if (!_world.IsTunnelOpen(a.x, a.y) || !_world.IsTunnelOpen(b.x, b.y)) return false;
            if (a == b) return true;

            // Cheap BFS; reuse seen set carefully — corridor BFS may have filled it
            var seen = new HashSet<long>();
            var q = new Queue<Vector2Int>();
            q.Enqueue(a);
            seen.Add(Key(a.x, a.y));
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            int guard = 0;
            while (q.Count > 0 && guard++ < MaxCorridorCells)
            {
                var cur = q.Dequeue();
                if (cur == b) return true;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cur.x + dx[k], ny = cur.y + dy[k];
                    if (!InBounds(nx, ny) || !_world.IsTunnelOpen(nx, ny)) continue;
                    long key = Key(nx, ny);
                    if (!seen.Add(key)) continue;
                    q.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return false;
        }

        Vector2Int SnapOpen(Vector2Int c)
        {
            if (InBounds(c.x, c.y) && _world.IsTunnelOpen(c.x, c.y)) return c;
            for (int r = 1; r <= 12; r++)
            for (int oy = -r; oy <= r; oy++)
            for (int ox = -r; ox <= r; ox++)
            {
                if (Mathf.Abs(ox) != r && Mathf.Abs(oy) != r) continue;
                int nx = c.x + ox, ny = c.y + oy;
                if (InBounds(nx, ny) && _world.IsTunnelOpen(nx, ny))
                    return new Vector2Int(nx, ny);
            }
            return c;
        }

        float LocalWidthScore(int x, int y)
        {
            int open = 0;
            for (int oy = -2; oy <= 2; oy++)
            for (int ox = -2; ox <= 2; ox++)
            {
                if (!InBounds(x + ox, y + oy)) continue;
                if (_world.IsTunnelOpen(x + ox, y + oy)) open++;
            }
            // More sensitive: corridors ~8–12 open still register
            float t = (open - 4) / 14f;
            return Mathf.Clamp(t * 7f, 0f, 7f);
        }

        float NearbySupportRelief(int x, int y)
        {
            Vector2 w = _world.CellCenter(x, y);
            float relief = 0f;
            float r = SupportRadiusWorld;
            float r2 = r * r;
            for (int i = 0; i < _supportWorld.Count; i++)
            {
                float d2 = (_supportWorld[i] - w).sqrMagnitude;
                if (d2 > r2) continue;
                float d = Mathf.Sqrt(d2);
                float q = i < _supportQuality.Count ? _supportQuality[i] : 0.8f;
                float cond = i < _supportCondition.Count ? _supportCondition[i] : 1f;
                relief += 5.5f * (1f - d / r) * Mathf.Clamp(q * cond, 0.3f, 1.2f);
            }
            return relief;
        }

        void SpawnPillarVisual(int x, int y)
        {
            if (_supportRoot == null || _world == null) return;
            var go = new GameObject($"Pillar_{x}_{y}");
            go.transform.SetParent(_supportRoot, false);

            // Nudge toward adjacent rock so the post reads as wall-mounted, not floating mid-floor
            Vector2 center = _world.CellCenter(x, y);
            Vector2 nudge = Vector2.zero;
            float cs = _world.CellSize;
            if (IsSolidCell(x + 1, y)) nudge.x += 1f;
            if (IsSolidCell(x - 1, y)) nudge.x -= 1f;
            if (IsSolidCell(x, y + 1)) nudge.y += 1f;
            if (IsSolidCell(x, y - 1)) nudge.y -= 1f;
            if (nudge.sqrMagnitude > 0.01f)
                center += nudge.normalized * (cs * 0.22f);

            go.transform.localPosition = center;
            // Footprint ~1.4 cells — readable but not bulky
            go.transform.localScale = Vector3.one * (cs * 1.45f);
            // Slight random yaw so pairs don't look stamped
            go.transform.localRotation = Quaternion.Euler(0f, 0f, ((x * 17 + y * 31) % 24) - 12f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _pillarSprite;
            sr.sortingOrder = 9;
            sr.color = Color.white;
            DigVisualKit.ApplyLit(sr);

            // Tiny work bulb on the open side — cozy accent, not corridor lighting
            AttachPillarBulb(go.transform, nudge);
        }

        void AttachPillarBulb(Transform pillar, Vector2 wallNudge)
        {
            if (pillar == null || _pillarBulbSprite == null) return;

            // Mount toward tunnel (opposite wall), slightly outside the post rim
            Vector2 open = wallNudge.sqrMagnitude > 0.01f
                ? -wallNudge.normalized
                : Vector2.down;
            var bulb = new GameObject("WorkBulb");
            bulb.transform.SetParent(pillar, false);
            bulb.transform.localPosition = new Vector3(open.x * 0.38f, open.y * 0.38f, 0f);
            bulb.transform.localScale = Vector3.one * 0.22f;

            var bsr = bulb.AddComponent<SpriteRenderer>();
            bsr.sprite = _pillarBulbSprite;
            bsr.sortingOrder = 10;
            var unlit = Shader.Find("Sprites/Default");
            if (unlit != null) bsr.sharedMaterial = new Material(unlit);
            bsr.color = new Color(1f, 0.82f, 0.48f, 0.92f);

            var light = bulb.AddComponent<Light2D>();
            DigVisualKit.ConfigurePointLight(light,
                new Color(1f, 0.72f, 0.38f),
                intensity: 0.28f,
                outer: 0.72f,
                inner: 0.04f,
                shadows: false,
                falloff: 0.82f);

            bulb.AddComponent<PillarBulbFlicker>().Bind(light, bsr, 0.28f);
        }

        /// <summary>
        /// Top-down industrial mine prop — iron foot, timber core, orange collar, bolts.
        /// Same palette / weathering language as DigVisualKit lanterns + YardVisualKit.
        /// </summary>
        static Sprite MakePillarSprite()
        {
            const int s = 48;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, Color.clear);

            Color outline = new(0.02f, 0.02f, 0.03f);
            Color black = new(0.06f, 0.06f, 0.07f);
            Color charcoal = new(0.14f, 0.15f, 0.16f);
            Color metal = new(0.42f, 0.44f, 0.48f);
            Color metalHi = new(0.68f, 0.7f, 0.74f);
            Color metalDeep = new(0.24f, 0.26f, 0.29f);
            Color orange = new(0.92f, 0.48f, 0.12f);
            Color orangeDeep = new(0.62f, 0.28f, 0.08f);
            Color rust = new(0.45f, 0.22f, 0.1f);
            Color timber = new(0.38f, 0.26f, 0.14f);
            Color timberHi = new(0.55f, 0.4f, 0.22f);
            Color timberDeep = new(0.22f, 0.14f, 0.08f);
            Color hazardY = new(0.95f, 0.82f, 0.12f);

            void Dot(int x, int y, Color c)
            {
                if ((uint)x < s && (uint)y < s) tex.SetPixel(x, y, c);
            }

            void Disc(float cx, float cy, float rx, float ry, Color fill, Color? edge = null)
            {
                int x0 = Mathf.FloorToInt(cx - rx - 1);
                int x1 = Mathf.CeilToInt(cx + rx + 1);
                int y0 = Mathf.FloorToInt(cy - ry - 1);
                int y1 = Mathf.CeilToInt(cy + ry + 1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x - cx) / Mathf.Max(0.01f, rx);
                    float dy = (y - cy) / Mathf.Max(0.01f, ry);
                    float d = dx * dx + dy * dy;
                    if (d > 1.02f) continue;
                    if (edge.HasValue && d > 0.78f) Dot(x, y, edge.Value);
                    else Dot(x, y, fill);
                }
            }

            void Box(int x0, int y0, int bw, int bh, Color c)
            {
                for (int y = y0; y < y0 + bh; y++)
                for (int x = x0; x < x0 + bw; x++)
                    Dot(x, y, c);
            }

            void Bolt(int x, int y)
            {
                Box(x - 1, y - 1, 3, 3, charcoal);
                Dot(x, y, metalHi);
            }

            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;

            // Iron foot plate (square with chamfered read via ring + box)
            Box(6, 6, 36, 36, outline);
            Box(7, 7, 34, 34, charcoal);
            Box(9, 9, 30, 30, metalDeep);
            // Plate edge highlight
            for (int i = 9; i < 39; i++)
            {
                Dot(i, 9, metal);
                Dot(9, i, metal);
                Dot(i, 38, black);
                Dot(38, i, black);
            }

            // Outer iron collar ring
            Disc(cx, cy, 15.5f, 15.5f, outline, null);
            Disc(cx, cy, 14.2f, 14.2f, metalDeep, outline);
            Disc(cx, cy, 12.5f, 12.5f, metal, null);

            // Orange hazard collar (industrial Deep Core accent)
            for (int a = 0; a < 12; a++)
            {
                float ang0 = a * Mathf.PI * 2f / 12f;
                float ang1 = (a + 0.55f) * Mathf.PI * 2f / 12f;
                Color band = (a % 2 == 0) ? orange : charcoal;
                for (float t = 0f; t <= 1f; t += 0.08f)
                {
                    float ang = Mathf.Lerp(ang0, ang1, t);
                    for (float r = 10.2f; r <= 12.2f; r += 0.5f)
                    {
                        int px = Mathf.RoundToInt(cx + Mathf.Cos(ang) * r);
                        int py = Mathf.RoundToInt(cy + Mathf.Sin(ang) * r);
                        Dot(px, py, band);
                        if (a % 2 == 0 && r > 11.4f) Dot(px, py, orangeDeep);
                    }
                }
            }

            // Timber post core (top-down end grain)
            Disc(cx, cy, 9.2f, 9.2f, outline, null);
            Disc(cx, cy, 8.2f, 8.2f, timberDeep, outline);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - cx) / 8.0f;
                float dy = (y - cy) / 8.0f;
                float d = dx * dx + dy * dy;
                if (d > 1f) continue;
                float n = Mathf.PerlinNoise(x * 0.35f + 2.1f, y * 0.35f);
                // Growth rings
                float ring = Mathf.Abs(Mathf.Sin(Mathf.Sqrt(d) * 14f + n));
                Color c = Color.Lerp(timberDeep, timber, 0.35f + 0.45f * n);
                c = Color.Lerp(c, timberHi, ring * 0.35f * (1f - d));
                if (d > 0.72f) c = Color.Lerp(c, outline, 0.55f);
                Dot(x, y, c);
            }

            // Center pith + steel cap plate
            Disc(cx, cy, 3.2f, 3.2f, charcoal, outline);
            Disc(cx, cy, 2.2f, 2.2f, metal, null);
            Dot(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy), metalHi);
            Dot(Mathf.RoundToInt(cx) - 1, Mathf.RoundToInt(cy) - 1, hazardY);

            // Foot bolts
            Bolt(12, 12);
            Bolt(35, 12);
            Bolt(12, 35);
            Bolt(35, 35);
            Bolt(23, 10);
            Bolt(23, 37);
            Bolt(10, 23);
            Bolt(37, 23);

            // Weather / rust
            for (int y = 7; y < 41; y++)
            for (int x = 7; x < 41; x++)
            {
                var c = tex.GetPixel(x, y);
                if (c.a < 0.1f) continue;
                float n = Mathf.PerlinNoise(x * 0.42f + 4f, y * 0.42f);
                if (n > 0.78f) tex.SetPixel(x, y, Color.Lerp(c, rust, 0.38f));
                else if (n < 0.18f) tex.SetPixel(x, y, Color.Lerp(c, black, 0.25f));
            }

            // Tiny amber work-bulb mark on the collar (top-down fixture cue)
            Disc(cx + 9.5f, cy - 2f, 1.6f, 1.6f, outline, null);
            Disc(cx + 9.5f, cy - 2f, 1.15f, 1.15f, new Color(1f, 0.78f, 0.35f), null);
            Dot(Mathf.RoundToInt(cx + 9.5f), Mathf.RoundToInt(cy - 2f), new Color(1f, 0.92f, 0.65f));

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        /// <summary>Very small warm glass blob for pillar work lights.</summary>
        static Sprite MakePillarBulbSprite()
        {
            const int s = 12;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float cx = (s - 1) * 0.5f, cy = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - cx) / 4.2f;
                float dy = (y - cy) / 4.2f;
                float d = dx * dx + dy * dy;
                if (d > 1f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }
                float t = 1f - d;
                var c = Color.Lerp(
                    new Color(0.85f, 0.45f, 0.12f, 0.55f),
                    new Color(1f, 0.95f, 0.7f, 1f),
                    t * t);
                if (d > 0.72f) c = Color.Lerp(c, new Color(0.15f, 0.1f, 0.05f, 0.9f), 0.45f);
                tex.SetPixel(x, y, c);
            }
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }

        void OnRegionChanged(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!InBounds(x, y)) continue;
                if (!_world.IsExcavated(x, y) && !_world.IsTunnelOpen(x, y)) continue;
                long key = Key(x, y);
                if (!_excavateHours.ContainsKey(key))
                    _excavateHours[key] = _gameHours;
            }
        }

        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
        bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h;
    }

    /// <summary>
    /// Pillar work-bulb: mostly steady warm glow; rare short flickers for mine atmosphere.
    /// </summary>
    public sealed class PillarBulbFlicker : MonoBehaviour
    {
        Light2D _light;
        SpriteRenderer _bulb;
        float _baseIntensity;
        Color _baseColor;
        float _nextFlickerIn;
        float _flickerLeft;
        float _phase;

        public void Bind(Light2D light, SpriteRenderer bulb, float baseIntensity)
        {
            _light = light;
            _bulb = bulb;
            _baseIntensity = baseIntensity;
            _baseColor = bulb != null ? bulb.color : Color.white;
            _phase = Random.Range(0f, 100f);
            ScheduleNext();
        }

        void ScheduleNext()
        {
            // Rare: roughly every 9–28s of real time
            _nextFlickerIn = Random.Range(9f, 28f);
            _flickerLeft = 0f;
        }

        void Update()
        {
            if (_light == null) return;
            float dt = Time.deltaTime;
            _phase += dt;

            // Very gentle breath so it doesn't look frozen — far quieter than CosyLantern
            float breath = 1f + 0.025f * Mathf.Sin(_phase * 0.85f + 0.4f);
            float mul = breath;

            if (_flickerLeft > 0f)
            {
                _flickerLeft -= dt;
                // Brief dip / stutter, not a disco
                float w = Mathf.PingPong(_flickerLeft * 22f, 1f);
                mul *= Mathf.Lerp(0.35f, 0.92f, w);
                if (_flickerLeft <= 0f)
                    ScheduleNext();
            }
            else
            {
                _nextFlickerIn -= dt;
                if (_nextFlickerIn <= 0f)
                    _flickerLeft = Random.Range(0.08f, 0.22f);
            }

            _light.intensity = _baseIntensity * mul;
            if (_bulb != null)
            {
                var c = _baseColor;
                c.a = _baseColor.a * Mathf.Clamp01(0.72f + 0.28f * mul);
                _bulb.color = c;
            }
        }
    }
}
