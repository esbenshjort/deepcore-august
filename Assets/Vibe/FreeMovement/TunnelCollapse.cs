using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    public enum TunnelStabilityBand : byte
    {
        Stable = 0,
        Watch = 1,
        Unstable = 2,
        Critical = 3,
    }

    public enum CollapseSeverity : byte
    {
        MinorDebris = 0,
        Blocking = 1,
        Major = 2,
    }

    public enum DebrisImpactZone : byte
    {
        Legs = 0,
        Overhead = 1,
        Lateral = 2,
        CatchFall = 3,
        Crush = 4,
    }

    /// <summary>One persistent rubble mass that may block navigation.</summary>
    public sealed class DebrisField
    {
        public int Id;
        public CollapseSeverity Severity;
        public Vector2 Epicenter;
        public readonly List<Vector2Int> Cells = new(16);
        public float WorkRemaining; // game-hours of clearance left
        public float WorkTotal;
        public bool BlocksNav;
        public bool Cleared;
        public float CreatedGameHours;
        public int HitWorkerCount;
        public Transform VisualRoot;
    }

    /// <summary>Player-facing collapse / rescue banner payload.</summary>
    public sealed class CollapseEventBanner
    {
        public string Title = "";
        public string Line1 = "";
        public string Line2 = "";
        public string Line3 = "";
        public float ExpireRealTime;
        public bool Critical;
    }

    /// <summary>
    /// Debris + tunnel collapse + trapped/incapacitated/rescue V1.
    /// Builds on MineInfrastructure instability, FineTerrainWorld walkability, and WorkerInjury.
    /// </summary>
    public sealed class TunnelCollapseSystem
    {
        public const float MinorClearHours = 0.35f;
        public const float BlockingClearHours = 1.6f;
        public const float MajorClearHours = 3.4f;
        public const float ExcavatorClearMul = 1.0f;
        public const float HaulerClearMul = 0.55f; // slower manual clear
        public const float RescueAssistHours = 0.45f;
        public const float RescueCarrySpeedMul = 0.42f;
        /// <summary>No natural warnings/collapses inside this radius of camp.</summary>
        public const float CampSafeRadiusWorld = 9.5f;
        /// <summary>Min distance from camp before a cell can be an end-tunnel collapse site.</summary>
        public const float EndTunnelMinCampDist = 7.5f;

        readonly FineTerrainWorld _world;
        readonly MineInfrastructure _infra;
        readonly Transform _fxRoot;
        readonly List<DebrisField> _fields = new(12);
        readonly Dictionary<long, int> _cellToField = new(128);
        readonly HashSet<long> _recentCollapseKeys = new();
        readonly List<Vector2Int> _scratchCells = new(32);
        readonly Queue<Vector2Int> _bfsQ = new(512);
        readonly HashSet<long> _bfsSeen = new();
        readonly List<CollapseEventBanner> _banners = new(4);

        Vector2 _campWorld;
        float _gameHours;
        float _warnCooldown;
        float _evalAccum;
        int _nextFieldId = 1;

        // Audit / DEV counters (natural sim only)
        public int AuditWarnings;
        public int AuditMinor;
        public int AuditBlocking;
        public int AuditMajor;
        public int AuditInjuries;
        public int AuditIncapacitations;
        public int AuditDeaths;
        public int AuditClears;
        public int AuditRescues;
        public int AuditTrappedMarks;

        public bool ShowStabilityOverlay;
        public IReadOnlyList<DebrisField> Fields => _fields;
        public IReadOnlyList<CollapseEventBanner> Banners => _banners;
        public CollapseEventBanner LastBanner { get; private set; }

        public TunnelCollapseSystem(FineTerrainWorld world, MineInfrastructure infra, Transform fxRoot)
        {
            _world = world;
            _infra = infra;
            _fxRoot = fxRoot;
        }

        public void BindCamp(Vector2 campWorld) => _campWorld = campWorld;

        public void SetGameHours(float absoluteGameHours) => _gameHours = absoluteGameHours;

        // ——— Stability ———

        public bool NearCamp(int x, int y)
        {
            if (_world == null) return true;
            return Vector2.Distance(_world.CellCenter(x, y), _campWorld) < CampSafeRadiusWorld;
        }

        /// <summary>
        /// End of a dug corridor: frontier tip (no open neighbor farther from camp)
        /// that still touches rock. Not yard chambers or mid-tunnel trunks.
        /// </summary>
        public bool IsEndTunnelSite(int x, int y)
        {
            if (_world == null || !_world.InBounds(x, y)) return false;
            if (!_world.IsTunnelOpen(x, y) && !_world.IsExcavated(x, y)) return false;
            Vector2 here = _world.CellCenter(x, y);
            float distCamp = Vector2.Distance(here, _campWorld);
            if (distCamp < EndTunnelMinCampDist) return false;

            int openMoore = 0;
            bool touchesRock = false;
            int fartherOpen = 0;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int nx = x + ox, ny = y + oy;
                if (!_world.InBounds(nx, ny))
                {
                    touchesRock = true;
                    continue;
                }
                bool open = _world.IsTunnelOpen(nx, ny) || _world.IsExcavated(nx, ny);
                if (open)
                {
                    openMoore++;
                    float nd = Vector2.Distance(_world.CellCenter(nx, ny), _campWorld);
                    if (nd > distCamp + 0.02f)
                        fartherOpen++;
                }
                else
                    touchesRock = true;
            }

            // Chamber / yard pad
            if (openMoore >= 7) return false;
            if (!touchesRock) return false;
            // Only the dig-face / cul-de-sac tip — mid-corridor always has a farther open neighbor
            return fartherOpen == 0;
        }

        public float RawRiskAt(int x, int y)
        {
            if (_world == null || !_world.InBounds(x, y) || !_world.IsTunnelOpen(x, y))
                return 0f;
            // Camp / yard / trunk corridors: never player-facing CRITICAL noise
            if (NearCamp(x, y) || !IsEndTunnelSite(x, y))
                return 0f;

            float inst = _infra != null ? _infra.EvaluateInstability(x, y) : 0f;
            // Map instability into a soft risk score; supports (quality-aware) already reduce EvaluateInstability.
            float risk = Mathf.Max(0f, inst - 0.8f);
            if (_infra != null)
            {
                float dist = _infra.NearestSupportDist(_world.CellCenter(x, y));
                if (dist > 2.4f) risk += (dist - 2.4f) * 0.45f;
                float q = _infra.NearestSupportQuality01(_world.CellCenter(x, y));
                if (q > 0.01f && dist < _infra.SupportRadiusWorld * 1.4f)
                    risk *= Mathf.Lerp(1.05f, 0.35f, Mathf.Clamp01(q));
            }
            // Recent collapse nearby raises risk
            long key = CellKey(x, y);
            if (_recentCollapseKeys.Contains(key)) risk += 2.2f;
            for (int oy = -2; oy <= 2; oy++)
            for (int ox = -2; ox <= 2; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                if (_recentCollapseKeys.Contains(CellKey(x + ox, y + oy)))
                    risk += 0.55f;
            }
            // Narrow unsupported tip: slight extra stress (not wide chambers)
            int open = 0;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (_world.IsTunnelOpen(x + ox, y + oy)) open++;
            }
            if (open <= 4) risk += 0.35f;
            return risk;
        }

        public TunnelStabilityBand BandAt(int x, int y)
        {
            float r = RawRiskAt(x, y);
            if (r < 1.2f) return TunnelStabilityBand.Stable;
            if (r < 2.6f) return TunnelStabilityBand.Watch;
            if (r < 4.4f) return TunnelStabilityBand.Unstable;
            return TunnelStabilityBand.Critical;
        }

        public string BandLabel(TunnelStabilityBand b) => b switch
        {
            TunnelStabilityBand.Watch => "WATCH",
            TunnelStabilityBand.Unstable => "UNSTABLE",
            TunnelStabilityBand.Critical => "CRITICAL",
            _ => "STABLE",
        };

        /// <summary>
        /// Natural tick — rare checks on end-tunnel stress only.
        /// Camp, yard chambers, and mid-corridor trunks never fire.
        /// </summary>
        public void TickNatural(
            float gameHoursDelta,
            IReadOnlyList<WorkerRuntime> crew,
            Func<WorkerRuntime, Vector2> worldPosOf,
            SocialMemoryStore memory,
            ManagerRelationshipStore managerRel)
        {
            if (_world == null || gameHoursDelta <= 0f) return;
            TickBanners();
            _warnCooldown = Mathf.Max(0f, _warnCooldown - gameHoursDelta);
            _evalAccum += gameHoursDelta;
            if (_evalAccum < 0.55f) return; // ~every half game-hour
            _evalAccum = 0f;

            TunnelStabilityBand worst = TunnelStabilityBand.Stable;
            Vector2Int worstCell = default;
            float worstRisk = 0f;
            void Consider(int x, int y)
            {
                if (!_world.InBounds(x, y) || !_world.IsTunnelOpen(x, y)) return;
                if (!IsEndTunnelSite(x, y)) return;
                float r = RawRiskAt(x, y);
                var band = BandAt(x, y);
                if (r > worstRisk)
                {
                    worstRisk = r;
                    worst = band;
                    worstCell = new Vector2Int(x, y);
                }
            }

            // Prefer workers who are actually out in end tunnels (not camp/yard staff)
            if (crew != null && worldPosOf != null)
            {
                for (int i = 0; i < crew.Count; i++)
                {
                    var wr = crew[i];
                    if (wr == null || !wr.IsAlive) continue;
                    var pos = worldPosOf(wr);
                    if (Vector2.Distance(pos, _campWorld) < EndTunnelMinCampDist) continue;
                    var c = _world.WorldToCell(pos);
                    Consider(c.x, c.y);
                    Consider(c.x + 1, c.y);
                    Consider(c.x - 1, c.y);
                    Consider(c.x, c.y + 1);
                    Consider(c.x, c.y - 1);
                    // Dig-face ring — one more step into the tip
                    Consider(c.x + 2, c.y);
                    Consider(c.x - 2, c.y);
                    Consider(c.x, c.y + 2);
                    Consider(c.x, c.y - 2);
                }
            }

            if (worst == TunnelStabilityBand.Stable || worstRisk < 1.4f)
                return;

            // Warnings rarer; long cooldown so banners don't spam
            float warnChance = worst switch
            {
                TunnelStabilityBand.Watch => 0.22f,
                TunnelStabilityBand.Unstable => 0.38f,
                TunnelStabilityBand.Critical => 0.28f,
                _ => 0f,
            };
            if (_warnCooldown <= 0f && WorkerRoll.NextUnit() < warnChance * 0.22f)
            {
                EmitWarning(worstCell, worst, crew, worldPosOf);
                _warnCooldown = 4.5f;
            }

            // Collapse fire — Watch never drops rock; Critical still uncommon
            float fire = worst switch
            {
                TunnelStabilityBand.Watch => 0f,
                TunnelStabilityBand.Unstable => 0.008f,
                TunnelStabilityBand.Critical => 0.022f,
                _ => 0f,
            };
            if (fire <= 0f || WorkerRoll.NextUnit() > fire) return;

            CollapseSeverity sev = PickSeverity(worst, worstRisk);
            TriggerCollapse(worstCell, sev, crew, worldPosOf, memory, managerRel, forced: false);
        }

        CollapseSeverity PickSeverity(TunnelStabilityBand band, float risk)
        {
            float u = WorkerRoll.NextUnit();
            if (band == TunnelStabilityBand.Critical || risk >= 5.5f)
            {
                if (u < 0.40f) return CollapseSeverity.Major;
                if (u < 0.78f) return CollapseSeverity.Blocking;
                return CollapseSeverity.MinorDebris;
            }
            if (band == TunnelStabilityBand.Unstable)
            {
                if (u < 0.18f) return CollapseSeverity.Major;
                if (u < 0.55f) return CollapseSeverity.Blocking;
                return CollapseSeverity.MinorDebris;
            }
            // Watch
            if (u < 0.08f) return CollapseSeverity.Blocking;
            return CollapseSeverity.MinorDebris;
        }

        void EmitWarning(
            Vector2Int cell,
            TunnelStabilityBand band,
            IReadOnlyList<WorkerRuntime> crew,
            Func<WorkerRuntime, Vector2> worldPosOf)
        {
            AuditWarnings++;
            Vector2 w = _world.CellCenter(cell.x, cell.y);
            FallingDebrisFx.SpawnCracking(w, _world.CellSize, _fxRoot);
            ExcavationDustFx.SpawnLingering(_fxRoot, w, _world.CellSize, heavy: false);
            DigHoodLog.Push($"TUNNEL | {BandLabel(band)} — dust and cracking near ({cell.x},{cell.y})");

            // Engineer concern if nearby
            if (crew == null || worldPosOf == null) return;
            for (int i = 0; i < crew.Count; i++)
            {
                var wr = crew[i];
                if (wr == null || !wr.IsAlive) continue;
                if (Vector2.Distance(worldPosOf(wr), w) > 4.5f) continue;
                DigHoodLog.Push($"ENGINEER WARN | {wr.DisplayName}: roof sounds wrong — {BandLabel(band)}");
                PushBanner("TUNNEL WARNING", BandLabel(band), $"{wr.DisplayName} hears cracking", "", false);
                break;
            }
        }

        // ——— Collapse ———

        public DebrisField TriggerCollapse(
            Vector2Int center,
            CollapseSeverity severity,
            IReadOnlyList<WorkerRuntime> crew,
            Func<WorkerRuntime, Vector2> worldPosOf,
            SocialMemoryStore memory,
            ManagerRelationshipStore managerRel,
            bool forced)
        {
            if (_world == null) return null;
            // Natural events: refuse camp / non–end-tunnel sites (DEV force still allowed)
            if (!forced && !IsEndTunnelSite(center.x, center.y))
                return null;

            _scratchCells.Clear();
            int radius = severity switch
            {
                CollapseSeverity.Major => 2,
                CollapseSeverity.Blocking => 1,
                _ => 0,
            };
            for (int oy = -radius; oy <= radius; oy++)
            for (int ox = -radius; ox <= radius; ox++)
            {
                if (Mathf.Abs(ox) + Mathf.Abs(oy) > radius + (severity == CollapseSeverity.Major ? 1 : 0))
                    continue;
                int x = center.x + ox, y = center.y + oy;
                if (!_world.InBounds(x, y)) continue;
                // Only collapse excavated open (or already open) cells — never invent solid rock
                if (!_world.IsExcavated(x, y) && !_world.IsTunnelOpen(x, y)) continue;
                if (_world.HasBlockingDebris(x, y)) continue;
                _scratchCells.Add(new Vector2Int(x, y));
            }
            if (_scratchCells.Count == 0 && _world.InBounds(center.x, center.y))
                _scratchCells.Add(center);

            bool blocks = severity != CollapseSeverity.MinorDebris;
            float work = severity switch
            {
                CollapseSeverity.Major => MajorClearHours,
                CollapseSeverity.Blocking => BlockingClearHours,
                _ => MinorClearHours,
            };

            var field = new DebrisField
            {
                Id = _nextFieldId++,
                Severity = severity,
                Epicenter = _world.CellCenter(center.x, center.y),
                WorkRemaining = work,
                WorkTotal = work,
                BlocksNav = blocks,
                Cleared = false,
                CreatedGameHours = _gameHours,
            };
            for (int i = 0; i < _scratchCells.Count; i++)
            {
                var c = _scratchCells[i];
                field.Cells.Add(c);
                _recentCollapseKeys.Add(CellKey(c.x, c.y));
                if (blocks)
                {
                    byte hp = severity == CollapseSeverity.Major ? (byte)8 : (byte)5;
                    _world.SetBlockingDebris(c.x, c.y, hp);
                    _cellToField[CellKey(c.x, c.y)] = field.Id;
                }
            }

            field.VisualRoot = FallingDebrisFx.SpawnCollapse(
                field.Epicenter, _world.CellSize, severity, _fxRoot, blocks);
            ExcavationDustFx.SpawnLingering(_fxRoot, field.Epicenter, _world.CellSize, heavy: true);
            _fields.Add(field);

            if (!forced)
            {
                if (severity == CollapseSeverity.MinorDebris) AuditMinor++;
                else if (severity == CollapseSeverity.Blocking) AuditBlocking++;
                else AuditMajor++;
            }

            // Soft-damage nearby supports (condition) — does not remove pillars
            _infra?.DamageSupportConditionNear(field.Epicenter, severity == CollapseSeverity.Major ? 0.22f : 0.10f);

            DigHoodLog.Push(
                $"TUNNEL COLLAPSE | {severity} @ ({center.x},{center.y}) cells={field.Cells.Count} block={blocks}");

            string cutName = ApplyImpacts(field, crew, worldPosOf, memory, managerRel, forced);
            RefreshTrappedFlags(crew, worldPosOf);

            if (severity == CollapseSeverity.MinorDebris)
            {
                PushBanner("ROCKFALL", "MINOR DEBRIS", string.IsNullOrEmpty(cutName) ? "Passage still open" : $"{cutName} hit", "", false);
            }
            else
            {
                PushBanner(
                    "TUNNEL COLLAPSE",
                    "ROUTE BLOCKED",
                    string.IsNullOrEmpty(cutName) ? "Clear debris to reopen" : $"{cutName.ToUpperInvariant()} CUT OFF FROM CAMP",
                    severity == CollapseSeverity.Major ? "MAJOR COLLAPSE" : "",
                    critical: true);
            }

            return field;
        }

        string ApplyImpacts(
            DebrisField field,
            IReadOnlyList<WorkerRuntime> crew,
            Func<WorkerRuntime, Vector2> worldPosOf,
            SocialMemoryStore memory,
            ManagerRelationshipStore managerRel,
            bool forced)
        {
            if (crew == null || worldPosOf == null) return "";
            string firstHit = "";
            float hitR = field.Severity switch
            {
                CollapseSeverity.Major => 2.1f,
                CollapseSeverity.Blocking => 1.35f,
                _ => 0.95f,
            };

            for (int i = 0; i < crew.Count; i++)
            {
                var wr = crew[i];
                if (wr == null || !wr.IsAlive || wr.State == null) continue;
                Vector2 pos = worldPosOf(wr);
                float d = Vector2.Distance(pos, field.Epicenter);
                if (d > hitR) continue;

                var zone = CollapseInjuryResolver.PickZone(pos, field.Epicenter, field.Severity);
                bool hit = CollapseInjuryResolver.RollHit(field.Severity, d, hitR);
                if (!hit)
                {
                    wr.Injuries?.NoteAccident($"{wr.DisplayName} near collapse — uninjured");
                    continue;
                }

                field.HitWorkerCount++;
                if (string.IsNullOrEmpty(firstHit)) firstHit = wr.DisplayName;

                var injuries = CollapseInjuryResolver.ResolveHits(wr, field.Severity, zone);
                if (!forced) AuditInjuries += injuries.Count;

                bool incap = false;
                for (int k = 0; k < injuries.Count; k++)
                {
                    var rec = injuries[k];
                    DigHoodLog.Push($"DEBRIS HIT | {wr.DisplayName} — {rec.DisplayName} ({zone})");
                }

                if (CollapseInjuryResolver.ShouldIncapacitate(wr, field.Severity, injuries))
                {
                    wr.State.MarkIncapacitated(_gameHours, "Tunnel collapse");
                    incap = true;
                    if (!forced) AuditIncapacitations++;
                    DigHoodLog.Push($"INCAPACITATED | {wr.DisplayName} — cannot move unaided");
                }

                // Extremely rare death — catastrophic major + critical trauma only
                if (field.Severity == CollapseSeverity.Major
                    && wr.State.Injury >= WorkerState.CriticalInjuryThreshold
                    && injuries.Exists(r => r.Severity >= WorkerInjurySeverity.Critical)
                    && WorkerRoll.NextUnit() < 0.04f)
                {
                    wr.State.MarkDead(_gameHours, 0);
                    if (!forced) AuditDeaths++;
                    DigHoodLog.Push($"DEAD | {wr.DisplayName} — catastrophic collapse trauma");
                    PushBanner("TUNNEL COLLAPSE", $"{wr.DisplayName.ToUpperInvariant()} LOST", "CATASTROPHIC TRAUMA", "", true);
                }
                else if (incap)
                {
                    PushBanner(
                        "TUNNEL COLLAPSE",
                        $"{wr.DisplayName.ToUpperInvariant()} HIT BY DEBRIS",
                        InjurySummary(injuries),
                        "INCAPACITATED",
                        true);
                }
                else if (injuries.Count > 0)
                {
                    PushBanner(
                        "DEBRIS HIT",
                        wr.DisplayName.ToUpperInvariant(),
                        InjurySummary(injuries),
                        "",
                        field.Severity != CollapseSeverity.MinorDebris);
                }

                // Frustration / morale / manager
                if (wr.State.IsAlive)
                {
                    wr.State.Frustration = Mathf.Min(100f, wr.State.Frustration + (field.Severity == CollapseSeverity.Major ? 18f : 8f));
                    ClaustrophobiaSystem.SpikeCollapse(wr,
                        field.Severity == CollapseSeverity.Major ? 28f : 16f);
                    wr.State.Morale = Mathf.Max(0f, wr.State.Morale - (field.Severity == CollapseSeverity.Major ? 10f : 4f));
                    managerRel?.Get(wr.WorkerId)?.Add(
                        -1.5f, 0f, field.Severity == CollapseSeverity.Major ? 4f : 1.5f);
                }

                RecordCollapseMemories(wr, field, crew, worldPosOf, memory, incap);
            }

            return firstHit;
        }

        static string InjurySummary(List<WorkerInjuryRecord> injuries)
        {
            if (injuries == null || injuries.Count == 0) return "UNINJURED";
            if (injuries.Count == 1) return injuries[0].DisplayName;
            return $"{injuries[0].DisplayName}\n{injuries[1].DisplayName}";
        }

        void RecordCollapseMemories(
            WorkerRuntime victim,
            DebrisField field,
            IReadOnlyList<WorkerRuntime> crew,
            Func<WorkerRuntime, Vector2> worldPosOf,
            SocialMemoryStore memory,
            bool incap)
        {
            if (memory == null || victim == null || field.Severity == CollapseSeverity.MinorDebris)
                return;

            float strength = field.Severity == CollapseSeverity.Major ? 0.72f : 0.55f;
            memory.Add(new SocialMemoryEntry
            {
                Type = SocialMemoryType.SurvivedCollapse,
                Strength = strength,
                GameTime = _gameHours,
                ObserverId = victim.WorkerId,
                TargetId = victim.WorkerId,
                Context = SocialContext.Emergency,
                SourceRef = $"collapse:{field.Id}",
                Significance = SocialMemoryStore.ClassifySignificance(strength, forceMajor: field.Severity == CollapseSeverity.Major),
            });

            if (incap || field.BlocksNav)
            {
                memory.Add(new SocialMemoryEntry
                {
                    Type = SocialMemoryType.WasTrapped,
                    Strength = 0.68f,
                    GameTime = _gameHours,
                    ObserverId = victim.WorkerId,
                    TargetId = victim.WorkerId,
                    Context = SocialContext.Emergency,
                    SourceRef = $"trap:{field.Id}",
                    Significance = SocialMemorySignificance.Major,
                });
            }

            // Witnesses nearby
            for (int i = 0; i < crew.Count; i++)
            {
                var w = crew[i];
                if (w == null || w.WorkerId == victim.WorkerId || !w.IsAlive) continue;
                if (Vector2.Distance(worldPosOf(w), field.Epicenter) > 5.5f) continue;
                memory.Add(new SocialMemoryEntry
                {
                    Type = SocialMemoryType.WitnessedSeriousAccident,
                    Strength = 0.58f,
                    GameTime = _gameHours,
                    ObserverId = w.WorkerId,
                    TargetId = victim.WorkerId,
                    Context = SocialContext.Emergency,
                    SourceRef = $"witness:{field.Id}",
                    Significance = SocialMemorySignificance.Significant,
                });
            }
        }

        // ——— Trapped / connectivity ———

        public bool IsReachableFromCamp(Vector2 worldPos)
        {
            if (_world == null) return true;
            return HasOpenPath(_campWorld, worldPos);
        }

        public void RefreshTrappedFlags(
            IReadOnlyList<WorkerRuntime> crew,
            Func<WorkerRuntime, Vector2> worldPosOf)
        {
            if (crew == null || worldPosOf == null) return;
            for (int i = 0; i < crew.Count; i++)
            {
                var wr = crew[i];
                if (wr?.State == null || !wr.IsAlive) continue;
                Vector2 pos = worldPosOf(wr);
                float distCamp = Vector2.Distance(pos, _campWorld);
                // Near camp is never "trapped" — avoids false positives collapsing morning commute
                if (distCamp <= 3.25f)
                {
                    wr.State.TrappedFromCamp = false;
                    continue;
                }
                bool trapped = !IsReachableFromCamp(pos);
                if (trapped && !wr.State.TrappedFromCamp)
                {
                    wr.State.TrappedFromCamp = true;
                    AuditTrappedMarks++;
                    DigHoodLog.Push($"TRAPPED | {wr.DisplayName} cut off from camp");
                    if (wr.State.Incapacitated)
                        PushBanner("ACCESS BLOCKED",
                            $"ACCESS TO {wr.DisplayName.ToUpperInvariant()} BLOCKED",
                            "Clear debris to reach them", "", true);
                }
                else if (!trapped)
                    wr.State.TrappedFromCamp = false;
            }
        }

        bool HasOpenPath(Vector2 fromWorld, Vector2 toWorld)
        {
            var a = SnapOpen(_world.WorldToCell(fromWorld));
            var b = SnapOpen(_world.WorldToCell(toWorld));
            if (!_world.InBounds(a.x, a.y) || !_world.InBounds(b.x, b.y)) return false;
            if (!_world.IsTunnelOpen(a.x, a.y) || !_world.IsTunnelOpen(b.x, b.y)) return false;
            if (a == b) return true;
            _bfsSeen.Clear();
            _bfsQ.Clear();
            _bfsQ.Enqueue(a);
            _bfsSeen.Add(CellKey(a.x, a.y));
            int guard = 0;
            while (_bfsQ.Count > 0 && guard++ < 6000)
            {
                var cur = _bfsQ.Dequeue();
                if (cur.x == b.x && cur.y == b.y) return true;
                TryEnqueue(cur.x + 1, cur.y);
                TryEnqueue(cur.x - 1, cur.y);
                TryEnqueue(cur.x, cur.y + 1);
                TryEnqueue(cur.x, cur.y - 1);
            }
            return false;
        }

        void TryEnqueue(int x, int y)
        {
            if (!_world.InBounds(x, y) || !_world.IsTunnelOpen(x, y)) return;
            long k = CellKey(x, y);
            if (!_bfsSeen.Add(k)) return;
            _bfsQ.Enqueue(new Vector2Int(x, y));
        }

        Vector2Int SnapOpen(Vector2Int c)
        {
            if (_world.InBounds(c.x, c.y) && _world.IsTunnelOpen(c.x, c.y)) return c;
            for (int r = 1; r <= 10; r++)
            for (int oy = -r; oy <= r; oy++)
            for (int ox = -r; ox <= r; ox++)
            {
                if (Mathf.Abs(ox) != r && Mathf.Abs(oy) != r) continue;
                int nx = c.x + ox, ny = c.y + oy;
                if (_world.InBounds(nx, ny) && _world.IsTunnelOpen(nx, ny))
                    return new Vector2Int(nx, ny);
            }
            return c;
        }

        // ——— Clearance ———

        public DebrisField FindNearestClearable(Vector2 from, float maxDist = 3.5f)
        {
            DebrisField best = null;
            float bestD = maxDist;
            for (int i = 0; i < _fields.Count; i++)
            {
                var f = _fields[i];
                if (f == null || f.Cleared || !f.BlocksNav) continue;
                float d = Vector2.Distance(from, f.Epicenter);
                if (d < bestD) { bestD = d; best = f; }
            }
            return best;
        }

        public bool CanClearDebris(WorkerRuntime wr, bool asExcavator)
        {
            if (wr?.State == null || !wr.IsAlive) return false;
            if (wr.State.Incapacitated) return false;
            if (WorkerInjuryConsequences.ForcesOutOfWork(wr.Injuries) && !asExcavator)
                return false;
            // Excavator also blocked if incapacitated (already) or dead
            return true;
        }

        /// <summary>
        /// Progress clearance. Excavator dig is faster; Hauler uses HeavyLifting/Stamina/Logistics.
        /// Returns true when field fully cleared.
        /// </summary>
        public bool TickClearance(
            DebrisField field,
            WorkerRuntime wr,
            float gameHoursDelta,
            bool asExcavator,
            out string status)
        {
            status = "";
            if (field == null || field.Cleared || wr?.State == null || gameHoursDelta <= 0f)
                return false;
            if (!CanClearDebris(wr, asExcavator))
            {
                status = wr.State.Incapacitated ? "INCAPACITATED — cannot clear" : "too injured";
                return false;
            }

            float mul = asExcavator ? ExcavatorClearMul : HaulerClearMul;
            if (!asExcavator)
            {
                float lift = wr.Stats.Get(WorkerStatId.HeavyLifting) / 20f;
                float stam = wr.Stats.Get(WorkerStatId.Stamina) / 20f;
                float logi = wr.Stats.Get(WorkerStatId.Logistics) / 20f;
                mul *= Mathf.Lerp(0.55f, 1.25f, lift * 0.5f + stam * 0.3f + logi * 0.2f);
                mul *= WorkerInjuryConsequences.ManualWorkMul(wr.Injuries);
                mul *= WorkerInjuryConsequences.LoadCarryMul(wr.Injuries);
            }
            else
            {
                float mech = wr.Stats.Get(WorkerStatId.Mechanics) / 20f;
                float power = wr.Stats.Get(WorkerStatId.RawPower) / 20f;
                mul *= Mathf.Lerp(0.7f, 1.2f, mech * 0.45f + power * 0.55f);
                mul *= WorkerInjuryConsequences.ManualWorkMul(wr.Injuries);
            }

            if (wr.State.ExhaustionLatched) mul *= 0.55f;
            float progress = gameHoursDelta * mul;
            field.WorkRemaining = Mathf.Max(0f, field.WorkRemaining - progress);

            // Stamina / fatigue cost
            float staminaTax = asExcavator ? 2.2f : 3.4f;
            wr.State.SpendStamina(staminaTax * gameHoursDelta * 10f);
            wr.State.Frustration = Mathf.Min(100f, wr.State.Frustration + gameHoursDelta * (asExcavator ? 2f : 5f));

            // Chip debris HP proportionally
            float done01 = 1f - field.WorkRemaining / Mathf.Max(0.01f, field.WorkTotal);
            int expectHp = field.Severity == CollapseSeverity.Major ? 8 : 5;
            byte targetHp = (byte)Mathf.Clamp(Mathf.CeilToInt(expectHp * (1f - done01)), 0, 15);
            for (int i = 0; i < field.Cells.Count; i++)
            {
                var c = field.Cells[i];
                if (!_world.HasBlockingDebris(c.x, c.y)) continue;
                byte cur = _world.GetDebrisHp(c.x, c.y);
                if (cur > targetHp)
                    _world.SetBlockingDebris(c.x, c.y, targetHp == 0 ? (byte)1 : targetHp);
            }

            status = asExcavator
                ? $"DIGGING DEBRIS {Mathf.Clamp01(done01) * 100f:0}%"
                : $"CLEARING DEBRIS {Mathf.Clamp01(done01) * 100f:0}%";

            if (field.WorkRemaining > 0.02f) return false;
            FinishClear(field);
            AuditClears++;
            status = "ROUTE OPEN";
            return true;
        }

        public void FinishClear(DebrisField field)
        {
            if (field == null || field.Cleared) return;
            field.Cleared = true;
            field.BlocksNav = false;
            field.WorkRemaining = 0f;
            for (int i = 0; i < field.Cells.Count; i++)
            {
                var c = field.Cells[i];
                _world.ClearBlockingDebris(c.x, c.y);
                _cellToField.Remove(CellKey(c.x, c.y));
            }
            if (field.VisualRoot != null)
                UnityEngine.Object.Destroy(field.VisualRoot.gameObject);
            DigHoodLog.Push($"DEBRIS CLEARED | field #{field.Id} — navigation restored");
            PushBanner("ROUTE OPEN", "DEBRIS CLEARED", "Passage restored", "", false);
        }

        public void ClearAllDebrisDev()
        {
            for (int i = 0; i < _fields.Count; i++)
                if (_fields[i] != null && !_fields[i].Cleared)
                    FinishClear(_fields[i]);
        }

        /// <summary>
        /// Clear collapse rubble near camp/yard so morning jobs and camp walking are not soft-locked.
        /// End-tunnel debris farther out is left alone.
        /// </summary>
        public int ClearDebrisNearCamp(float radiusWorld)
        {
            if (_world == null) return 0;
            float r = Mathf.Max(1f, radiusWorld);
            float r2 = r * r;
            int n = 0;
            for (int i = 0; i < _fields.Count; i++)
            {
                var f = _fields[i];
                if (f == null || f.Cleared) continue;
                if ((f.Epicenter - _campWorld).sqrMagnitude > r2) continue;
                FinishClear(f);
                n++;
            }
            // Orphan debris cells (no field) near camp
            var c0 = _world.WorldToCell(_campWorld);
            int cellR = Mathf.CeilToInt(r / Mathf.Max(0.01f, _world.CellSize)) + 1;
            for (int oy = -cellR; oy <= cellR; oy++)
            for (int ox = -cellR; ox <= cellR; ox++)
            {
                int x = c0.x + ox, y = c0.y + oy;
                if (!_world.HasBlockingDebris(x, y)) continue;
                Vector2 w = _world.CellCenter(x, y);
                if ((w - _campWorld).sqrMagnitude > r2) continue;
                _world.ClearBlockingDebris(x, y);
                _cellToField.Remove(CellKey(x, y));
                n++;
            }
            return n;
        }

        /// <summary>When excavator digs, also chip adjacent debris.</summary>
        public void NotifyExcavatorDig(Vector2 digWorld, WorkerRuntime wr, float gameHoursHint = 0.04f)
        {
            var f = FindNearestClearable(digWorld, 1.6f);
            if (f == null || wr == null) return;
            TickClearance(f, wr, gameHoursHint, asExcavator: true, out _);
        }

        // ——— Rescue ———

        public bool TryRescueTick(
            WorkerRuntime rescuer,
            WorkerRuntime casualty,
            float gameHoursDelta,
            Func<WorkerRuntime, Vector2> worldPosOf,
            Action<WorkerRuntime, Vector2> setWorldPos,
            SocialMemoryStore memory,
            out string status)
        {
            status = "";
            if (rescuer?.State == null || casualty?.State == null) return false;
            if (!rescuer.IsAlive || !casualty.IsAlive) return false;
            if (!casualty.State.Incapacitated) { status = "not incapacitated"; return false; }
            if (rescuer.State.Incapacitated) { status = "rescuer incapacitated"; return false; }

            Vector2 a = worldPosOf(rescuer);
            Vector2 b = worldPosOf(casualty);
            if (!IsReachableFromCamp(a) && Vector2.Distance(a, b) > 1.2f)
            {
                // Rescuer must be able to reach casualty through open path
            }
            if (!HasOpenPath(a, b))
            {
                status = "ACCESS BLOCKED";
                return false;
            }

            float dist = Vector2.Distance(a, b);
            if (dist > 0.55f)
            {
                // Walk toward casualty
                Vector2 step = Vector2.MoveTowards(a, b, 1.1f * gameHoursDelta * 8f);
                setWorldPos(rescuer, step);
                status = $"REACHING {casualty.DisplayName}";
                return false;
            }

            // Assist then haul toward camp
            float lift = rescuer.Stats.Get(WorkerStatId.HeavyLifting) / 20f;
            float speed = RescueCarrySpeedMul * Mathf.Lerp(0.7f, 1.25f, lift);
            speed *= WorkerInjuryConsequences.LoadCarryMul(rescuer.Injuries);
            Vector2 camp = _campWorld;
            Vector2 next = Vector2.MoveTowards(b, camp, speed * gameHoursDelta * 10f);
            setWorldPos(casualty, next);
            setWorldPos(rescuer, next + (a - b).normalized * 0.15f);
            rescuer.State.SpendStamina(4f * gameHoursDelta * 10f);
            status = $"RESCUING {casualty.DisplayName}";

            if (Vector2.Distance(next, camp) < 1.4f || IsReachableFromCamp(next) && Vector2.Distance(next, camp) < 2.5f)
            {
                // Arrived near camp — clear incapacitated movement lock but KEEP injuries
                casualty.State.ClearIncapacitated();
                casualty.State.NeedsCare = true;
                casualty.State.TrappedFromCamp = false;
                AuditRescues++;
                DigHoodLog.Push($"RESCUE | {rescuer.DisplayName} recovered {casualty.DisplayName} — injuries remain");
                PushBanner("RESCUED", casualty.DisplayName.ToUpperInvariant(),
                    $"Recovered by {rescuer.DisplayName}", "INJURIES REMAIN — NEEDS CARE", true);

                memory?.Add(new SocialMemoryEntry
                {
                    Type = SocialMemoryType.RescuedByWorker,
                    Strength = 0.78f,
                    GameTime = _gameHours,
                    ObserverId = casualty.WorkerId,
                    TargetId = rescuer.WorkerId,
                    Context = SocialContext.Emergency,
                    SourceRef = $"rescue:{casualty.WorkerId}",
                    Significance = SocialMemorySignificance.Major,
                });
                memory?.Add(new SocialMemoryEntry
                {
                    Type = SocialMemoryType.HelpedMe,
                    Strength = 0.7f,
                    GameTime = _gameHours,
                    ObserverId = casualty.WorkerId,
                    TargetId = rescuer.WorkerId,
                    Context = SocialContext.Emergency,
                    SourceRef = $"rescuehelp:{casualty.WorkerId}",
                    Significance = SocialMemorySignificance.Significant,
                });
                status = "RESCUE COMPLETE";
                return true;
            }
            return false;
        }

        public WorkerRuntime FindIncapacitatedNeedingRescue(
            IReadOnlyList<WorkerRuntime> crew,
            Func<WorkerRuntime, Vector2> worldPosOf)
        {
            if (crew == null) return null;
            for (int i = 0; i < crew.Count; i++)
            {
                var wr = crew[i];
                if (wr?.State == null || !wr.IsAlive) continue;
                if (wr.State.Incapacitated) return wr;
            }
            return null;
        }

        // ——— Presentation ———

        void PushBanner(string title, string l1, string l2, string l3, bool critical)
        {
            var b = new CollapseEventBanner
            {
                Title = title,
                Line1 = l1,
                Line2 = l2,
                Line3 = l3,
                ExpireRealTime = Time.unscaledTime + (critical ? 7.5f : 4.5f),
                Critical = critical,
            };
            LastBanner = b;
            _banners.Add(b);
            while (_banners.Count > 3) _banners.RemoveAt(0);
        }

        void TickBanners()
        {
            float now = Time.unscaledTime;
            for (int i = _banners.Count - 1; i >= 0; i--)
                if (_banners[i].ExpireRealTime < now)
                    _banners.RemoveAt(i);
        }

        public void WeakenAreaDev(Vector2Int cell)
        {
            _recentCollapseKeys.Add(CellKey(cell.x, cell.y));
            for (int oy = -2; oy <= 2; oy++)
            for (int ox = -2; ox <= 2; ox++)
                _recentCollapseKeys.Add(CellKey(cell.x + ox, cell.y + oy));
            DigHoodLog.Push($"DEV | WEAKEN AREA ({cell.x},{cell.y})");
        }

        static long CellKey(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
