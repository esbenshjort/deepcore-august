using System.Collections.Generic;
using UnityEngine;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Worker navigation façade over <see cref="TunnelPathfinder"/>.
    /// Keeps Follow / Invalidate for crew, engineer, and prospector callers.
    /// </summary>
    public sealed class ExcavatedPathfinder
    {
        FineTerrainWorld _world;
        TunnelPathfinder _engine;
        PathAgentProfile _agent;
        readonly List<Vector2> _path = new(128);
        int _pathI;
        Vector2 _goal;
        float _repathT;
        float _arriveRadius = 0.04f;
        bool _stranded;
        bool _campReturnMode;
        bool _campHomeLogged;
        float _lateralOffset;

        public ExcavatedPathfinder(FineTerrainWorld world, float agentRadiusWorld = 0.12f)
        {
            Bind(world);
            SetAgentRadius(agentRadiusWorld);
        }

        public ExcavatedPathfinder(TunnelNavGrid nav, float agentRadiusWorld = 0.12f)
        {
            _world = nav?.World;
            _engine = new TunnelPathfinder(nav);
            SetAgentRadius(agentRadiusWorld);
            Invalidate();
        }

        public TunnelPathfinder Engine => _engine;
        public bool Stranded => _stranded;
        public bool CampReturnMode
        {
            get => _campReturnMode;
            set => _campReturnMode = value;
        }

        /// <summary>Persistent lateral path stagger (world units). Applied where clearance allows.</summary>
        public float LateralOffset
        {
            get => _lateralOffset;
            set
            {
                _lateralOffset = value;
                RefreshAgentProfile();
            }
        }

        public void Bind(FineTerrainWorld world)
        {
            _world = world;
            _engine = world != null ? new TunnelPathfinder(world.Navigation) : null;
            RefreshAgentProfile();
            Invalidate();
        }

        public void SetAgentRadius(float bodyRadius)
        {
            _agent.BodyRadius = bodyRadius;
            _agent.MinClearance = Mathf.Max(1, _agent.MinClearance);
            RefreshAgentProfile();
        }

        void RefreshAgentProfile()
        {
            float r = _agent.BodyRadius > 0.001f ? _agent.BodyRadius : 0.12f;
            _agent = PathAgentProfile.Worker(r, _lateralOffset);
        }

        public void Invalidate()
        {
            _path.Clear();
            _pathI = 0;
            _goal = new Vector2(float.NaN, float.NaN);
            _repathT = 0f;
            _stranded = false;
            _campHomeLogged = false;
        }

        /// <summary>Step one frame toward goal along excavated tunnel. Returns true when close enough.</summary>
        public bool Follow(Vector2 from, Vector2 goal, float moveSpeed, float bodyRadius,
            System.Action<Vector2> face, System.Func<Vector2, float, bool> tryStep)
        {
            if (_world == null || _engine == null) return true;

            float arrive = Mathf.Max(_arriveRadius, bodyRadius * 0.9f);
            if ((goal - from).sqrMagnitude <= arrive * arrive)
            {
                Invalidate();
                return true;
            }

            if (_stranded && _campReturnMode)
                return false;

            _repathT -= Time.deltaTime;
            bool goalMoved = float.IsNaN(_goal.x) || (goal - _goal).sqrMagnitude > 0.06f * 0.06f;
            if (_path.Count == 0 || _pathI >= _path.Count || goalMoved || _repathT <= 0f)
            {
                RebuildPath(from, goal);
                _goal = goal;
                _repathT = 0.45f;
            }

            if (_stranded && _campReturnMode)
                return false;

            Vector2 waypoint = goal;
            if (_path.Count > 0 && _pathI < _path.Count)
            {
                waypoint = _path[_pathI];
                float wpR = Mathf.Max(0.03f, _world.CellSize * 0.55f);
                if ((waypoint - from).sqrMagnitude < wpR * wpR)
                {
                    _pathI++;
                    waypoint = _pathI < _path.Count ? _path[_pathI] : goal;
                }
            }

            Vector2 dir = waypoint - from;
            if (dir.sqrMagnitude < 0.00001f) return false;
            dir.Normalize();
            face?.Invoke(dir);

            float step = moveSpeed * Time.deltaTime;
            if (tryStep != null)
            {
                if (!tryStep(dir, step) &&
                    !tryStep(new Vector2(dir.x, 0f), step) &&
                    !tryStep(new Vector2(0f, dir.y), step))
                {
                    Vector2 perp = new(-dir.y, dir.x);
                    if (!tryStep((dir + perp * 0.7f).normalized, step) &&
                        !tryStep((dir - perp * 0.7f).normalized, step))
                        _repathT = 0f;
                }
            }

            return (goal - from).sqrMagnitude <= arrive * arrive;
        }

        void RebuildPath(Vector2 from, Vector2 to)
        {
            _path.Clear();
            _pathI = 0;
            _stranded = false;
            if (_engine == null) return;

            RefreshAgentProfile();
            PathResult result;
            // Camp return: one FindPathHome (rebuild/retry/STRANDED logs). Later repaths use normal FindPath.
            if (_campReturnMode && !_campHomeLogged)
            {
                result = _engine.FindPathHome(from, to, _agent, PathCostPolicy.Default);
                _campHomeLogged = true;
            }
            else
                result = _engine.FindPath(from, to, _agent, PathCostPolicy.Default);

            if (result.Stranded)
            {
                _stranded = true;
                return;
            }

            if (!result.Success || result.Waypoints.Count == 0)
            {
                // If home mode already tried FindPathHome and later repaths fail, mark stranded.
                if (_campReturnMode && _campHomeLogged)
                    _stranded = true;
                return;
            }

            for (int i = 0; i < result.Waypoints.Count; i++)
                _path.Add(result.Waypoints[i]);
        }

        public bool TryFindPath(Vector2 fromWorld, Vector2 toWorld, List<Vector2> outWaypoints)
        {
            outWaypoints?.Clear();
            if (_engine == null) return false;
            RefreshAgentProfile();
            var r = _engine.FindPath(fromWorld, toWorld, _agent, PathCostPolicy.Default);
            if (!r.Success) return false;
            if (outWaypoints != null)
            {
                for (int i = 0; i < r.Waypoints.Count; i++)
                    outWaypoints.Add(r.Waypoints[i]);
            }
            return true;
        }

        public PathResult FindPathHome(Vector2 fromWorld, Vector2 campWorld)
        {
            if (_engine == null)
            {
                var fail = new PathResult { Stranded = true, DebugNote = "no engine" };
                DigHoodLog.Push("PATH HOME | STRANDED");
                return fail;
            }
            RefreshAgentProfile();
            return _engine.FindPathHome(fromWorld, campWorld, _agent, PathCostPolicy.Default);
        }
    }
}
