using System.Collections.Generic;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// F0.5: WorkerId → WorkerAvatar. Runner-owned; WorkerRuntime stays plain data.
    /// </summary>
    public sealed class WorkerPresenceRegistry
    {
        readonly Dictionary<int, WorkerAvatar> _byId = new(8);

        public IReadOnlyDictionary<int, WorkerAvatar> All => _byId;

        public void Clear()
        {
            foreach (var kv in _byId)
            {
                if (kv.Value != null)
                    UnityEngine.Object.Destroy(kv.Value.gameObject);
            }
            _byId.Clear();
        }

        public void Register(WorkerAvatar avatar)
        {
            if (avatar == null || avatar.WorkerId <= 0) return;
            _byId[avatar.WorkerId] = avatar;
        }

        public bool TryGet(int workerId, out WorkerAvatar avatar) =>
            _byId.TryGetValue(workerId, out avatar) && avatar != null;

        public WorkerAvatar Get(int workerId) =>
            TryGet(workerId, out var a) ? a : null;

        public WorkerAvatar Get(WorkerRuntime worker) =>
            worker != null ? Get(worker.WorkerId) : null;
    }
}
