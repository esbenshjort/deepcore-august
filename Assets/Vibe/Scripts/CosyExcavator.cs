using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DeepCore.Vibe
{
    /// <summary>
    /// Slow, cosy excavator: click floor to crawl, click rock to drill.
    /// </summary>
    public sealed class CosyExcavator : MonoBehaviour
    {
        [Header("Cosy feel")]
        [SerializeField] float moveSpeed = 1.35f;
        [SerializeField] float drillSecondsPerCell = 3.2f;
        [SerializeField] float drillShake = 0.045f;
        [SerializeField] float drillLightPulse = 0.55f;

        CaveGrid _grid;
        Transform _footprintRoot;
        Vector2Int _cell;
        bool _hasPendingDrill;
        int _footprint = 3;

        enum Mode { Idle, Moving, Drilling }
        Mode _mode;
        Vector2Int _drillCell;
        float _drillProgress;
        readonly List<Vector2Int> _path = new();
        int _pathIndex;

        GameObject _drillFx;
        Light2D _drillLight;
        SpriteRenderer _drillTargetTint;
        Color _drillBaseColor = Color.white;

        public void ConfigureFeel(float moveSpeed, float drillSeconds, int footprint = 3)
        {
            this.moveSpeed = moveSpeed;
            drillSecondsPerCell = drillSeconds;
            _footprint = footprint;
        }

        public void Setup(CaveGrid grid, Vector2Int startCell, Transform footprintRoot)
        {
            _grid = grid;
            _cell = startCell;
            _footprintRoot = footprintRoot;
            transform.position = _grid.CellToWorld(_cell.x, _cell.y);
            SyncFootprint();
        }

        void Update()
        {
            if (_grid == null) return;
            HandleInput();
            TickMove();
            TickDrill();
        }

        void HandleInput()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return;

            if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                CancelAction();
                return;
            }

            // Don't steal clicks while space-panning the camera
            if (keyboard != null && keyboard.spaceKey.isPressed) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            world.z = 0f;
            Vector2Int clicked = _grid.WorldToCell(world);
            if (!_grid.InBounds(clicked.x, clicked.y)) return;

            if (_grid.IsRock(clicked.x, clicked.y))
                CommandDrill(clicked);
            else
                CommandMove(clicked);
        }

        void CommandMove(Vector2Int target)
        {
            _hasPendingDrill = false;
            if (!_grid.CanFitUnit(target.x, target.y, _footprint))
            {
                if (!TryFindNearbyStance(target, out target))
                    return;
            }

            if (!BuildPath(_cell, target))
                return;

            _mode = Mode.Moving;
            _pathIndex = 0;
            StopDrillFx();
        }

        void CommandDrill(Vector2Int rock)
        {
            if (_grid.IsAdjacentToFootprint(rock.x, rock.y, _cell.x, _cell.y, _footprint))
            {
                BeginDrill(rock);
                return;
            }

            if (!TryFindDrillStance(rock, out var stance))
                return;

            if (!BuildPath(_cell, stance))
                return;

            _mode = Mode.Moving;
            _pathIndex = 0;
            _drillCell = rock;
            _hasPendingDrill = true;
            StopDrillFx();
        }

        void BeginDrill(Vector2Int rock)
        {
            if (!_grid.IsRock(rock.x, rock.y)) return;
            if (!_grid.IsAdjacentToFootprint(rock.x, rock.y, _cell.x, _cell.y, _footprint)) return;

            _mode = Mode.Drilling;
            _drillCell = rock;
            _drillProgress = 0f;
            _path.Clear();
            StartDrillFx(rock);
        }

        void TickMove()
        {
            if (_mode != Mode.Moving) return;
            if (_pathIndex >= _path.Count)
            {
                var pending = _drillCell;
                bool resume = _hasPendingDrill;
                _hasPendingDrill = false;
                _mode = Mode.Idle;
                if (resume &&
                    _grid.IsRock(pending.x, pending.y) &&
                    _grid.IsAdjacentToFootprint(pending.x, pending.y, _cell.x, _cell.y, _footprint))
                {
                    BeginDrill(pending);
                }
                return;
            }

            Vector2Int next = _path[_pathIndex];
            Vector3 dest = _grid.CellToWorld(next.x, next.y);
            transform.position = Vector3.MoveTowards(transform.position, dest, moveSpeed * Time.deltaTime);
            SyncFootprint();

            // Soft facing toward travel
            Vector3 delta = dest - transform.position;
            if (delta.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.Euler(0f, 0f, angle),
                    120f * Time.deltaTime);
            }

            if ((transform.position - dest).sqrMagnitude < 0.001f)
            {
                _cell = next;
                transform.position = dest;
                _pathIndex++;
                SyncFootprint();
            }
        }

        void TickDrill()
        {
            if (_mode != Mode.Drilling) return;
            if (!_grid.IsRock(_drillCell.x, _drillCell.y))
            {
                CancelAction();
                return;
            }

            _drillProgress += Time.deltaTime / Mathf.Max(0.1f, drillSecondsPerCell);
            float t = Mathf.Clamp01(_drillProgress);

            // Cosy micro-shake + warm pulse
            float shake = Mathf.Sin(Time.time * 18f) * drillShake * (0.35f + t);
            Vector3 basePos = _grid.CellToWorld(_cell.x, _cell.y);
            transform.position = basePos + new Vector3(shake, -shake * 0.6f, 0f);
            SyncFootprint();

            // Face the rock
            Vector3 rockPos = _grid.CellToWorld(_drillCell.x, _drillCell.y);
            Vector3 dir = rockPos - basePos;
            if (dir.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.Euler(0f, 0f, angle),
                    90f * Time.deltaTime);
            }

            if (_drillTargetTint != null)
            {
                // Darken / warm crack as progress climbs
                _drillTargetTint.color = Color.Lerp(
                    _drillBaseColor,
                    new Color(0.55f, 0.35f, 0.2f, 1f),
                    t * 0.85f);
            }

            if (_drillLight != null)
                _drillLight.intensity = 0.7f + Mathf.Sin(Time.time * 6f) * drillLightPulse;

            if (_drillFx != null)
            {
                float s = 0.7f + t * 0.5f + Mathf.Sin(Time.time * 10f) * 0.05f;
                _drillFx.transform.localScale = Vector3.one * s;
            }

            if (t >= 1f)
            {
                _grid.TryExcavate(_drillCell.x, _drillCell.y);
                transform.position = basePos;
                CancelAction();
            }
        }

        void StartDrillFx(Vector2Int rock)
        {
            StopDrillFx();
            Vector3 pos = _grid.CellToWorld(rock.x, rock.y);

            _drillFx = new GameObject("DrillFx");
            _drillFx.transform.position = pos;
            var sr = _drillFx.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.SoftLightCookie(48);
            sr.color = new Color(1f, 0.7f, 0.3f, 0.35f);
            sr.sortingOrder = 40;

            _drillLight = _drillFx.AddComponent<Light2D>();
            _drillLight.lightType = Light2D.LightType.Point;
            _drillLight.color = new Color(1f, 0.75f, 0.4f, 1f);
            _drillLight.intensity = 1f;
            _drillLight.pointLightInnerRadius = 0.2f;
            _drillLight.pointLightOuterRadius = 2.2f;
            _drillLight.falloffIntensity = 0.5f;

            var visual = _grid.GetCellVisual(rock.x, rock.y);
            if (visual != null)
            {
                _drillTargetTint = visual.GetComponent<SpriteRenderer>();
                if (_drillTargetTint != null)
                    _drillBaseColor = _drillTargetTint.color;
            }
        }

        void StopDrillFx()
        {
            if (_drillTargetTint != null)
            {
                _drillTargetTint.color = _drillBaseColor;
                _drillTargetTint = null;
            }
            if (_drillFx != null)
            {
                Destroy(_drillFx);
                _drillFx = null;
                _drillLight = null;
            }
        }

        void CancelAction()
        {
            _mode = Mode.Idle;
            _hasPendingDrill = false;
            _path.Clear();
            _pathIndex = 0;
            _drillProgress = 0f;
            transform.position = _grid.CellToWorld(_cell.x, _cell.y);
            StopDrillFx();
            SyncFootprint();
        }

        void SyncFootprint()
        {
            if (_footprintRoot == null) return;
            _footprintRoot.position = new Vector3(transform.position.x, transform.position.y, 0f);
        }

        bool TryFindNearbyStance(Vector2Int near, out Vector2Int stance)
        {
            stance = near;
            int best = int.MaxValue;
            bool found = false;
            for (int r = 0; r <= 4; r++)
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int x = near.x + dx;
                int y = near.y + dy;
                if (!_grid.CanFitUnit(x, y, _footprint)) continue;
                int d = Mathf.Abs(dx) + Mathf.Abs(dy);
                if (d >= best) continue;
                best = d;
                stance = new Vector2Int(x, y);
                found = true;
            }
            return found;
        }

        bool TryFindDrillStance(Vector2Int rock, out Vector2Int stance)
        {
            stance = _cell;
            int best = int.MaxValue;
            bool found = false;

            // Candidate centers whose footprint touches the rock orthogonally
            int reach = _footprint + 1;
            for (int y = rock.y - reach; y <= rock.y + reach; y++)
            for (int x = rock.x - reach; x <= rock.x + reach; x++)
            {
                if (!_grid.CanFitUnit(x, y, _footprint)) continue;
                if (!_grid.IsAdjacentToFootprint(rock.x, rock.y, x, y, _footprint)) continue;
                int d = Mathf.Abs(x - _cell.x) + Mathf.Abs(y - _cell.y);
                if (d >= best) continue;
                best = d;
                stance = new Vector2Int(x, y);
                found = true;
            }
            return found;
        }

        bool BuildPath(Vector2Int from, Vector2Int to)
        {
            _path.Clear();
            if (from == to) return true;
            if (!_grid.CanFitUnit(to.x, to.y, _footprint)) return false;

            // BFS on cells where 3x3 fits
            var came = new Dictionary<Vector2Int, Vector2Int>();
            var q = new Queue<Vector2Int>();
            q.Enqueue(from);
            came[from] = from;
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur == to) break;
                foreach (var d in dirs)
                {
                    var n = cur + d;
                    if (came.ContainsKey(n)) continue;
                    if (!_grid.CanFitUnit(n.x, n.y, _footprint)) continue;
                    came[n] = cur;
                    q.Enqueue(n);
                }
            }

            if (!came.ContainsKey(to)) return false;

            var stack = new List<Vector2Int>();
            var step = to;
            while (step != from)
            {
                stack.Add(step);
                step = came[step];
            }
            stack.Reverse();
            _path.AddRange(stack);
            return true;
        }

        void OnGUI()
        {
            if (_mode != Mode.Drilling) return;
            float t = Mathf.Clamp01(_drillProgress);
            var r = new Rect(12, 54, 220, 14);
            GUI.color = new Color(0.2f, 0.15f, 0.1f, 0.7f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.7f, 0.3f, 0.95f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * t, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
