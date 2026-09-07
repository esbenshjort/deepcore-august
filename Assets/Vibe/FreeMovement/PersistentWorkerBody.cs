using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeepCore.FreeMovement
{
    /// <summary>
    /// Persistent Worker Body V1 — one living WorkerRuntime = one visible WorkerAvatar.
    /// Job hosts are equipment / FSM destinations; their person sprites must not duplicate the avatar.
    /// Excavator is the machine exception (avatar hides while operating).
    /// </summary>
    public static class PersistentWorkerBody
    {
        public static bool IsMachineCabinJob(JobType job) => job == JobType.Excavation;

        /// <summary>
        /// Hide/show the operator person layer on a job host without touching equipment
        /// (cart load slots, excavator chassis, radar cone parent, etc.).
        /// </summary>
        public static void SetOperatorBodyVisible(Component host, bool on)
        {
            if (host == null) return;
            // Excavator chassis IS the equipment — never treat as operator body
            if (host is FreeWorkerController) return;

            Transform facing = host.transform.Find("Facing");
            Transform body = facing != null ? facing.Find("Body") : host.transform.Find("Body");
            if (body == null) return;

            foreach (var r in body.GetComponentsInChildren<SpriteRenderer>(true))
                r.enabled = on;
            foreach (var l in body.GetComponentsInChildren<Light2D>(true))
                l.enabled = on;
        }

        /// <summary>At spawn: hosts must not look like a second crew member.</summary>
        public static void HideOperatorBodiesOnHosts(
            FreeWorkerController excavator,
            ProspectorPerson prospector,
            HaulerPerson hauler,
            RefinerPerson refiner,
            EngineerPerson engineer,
            StewardPerson steward)
        {
            _ = excavator; // machine stays visible
            SetOperatorBodyVisible(prospector, false);
            SetOperatorBodyVisible(hauler, false);
            SetOperatorBodyVisible(refiner, false);
            SetOperatorBodyVisible(engineer, false);
            SetOperatorBodyVisible(steward, false);
        }

        public static void SetOperatorBodyVisibleForJob(
            JobType job,
            FreeWorkerController excavator,
            ProspectorPerson prospector,
            HaulerPerson hauler,
            RefinerPerson refiner,
            EngineerPerson engineer,
            StewardPerson steward,
            bool on)
        {
            switch (job)
            {
                case JobType.Prospecting: SetOperatorBodyVisible(prospector, on); break;
                case JobType.Hauling: SetOperatorBodyVisible(hauler, on); break;
                case JobType.Refining: SetOperatorBodyVisible(refiner, on); break;
                case JobType.Engineering: SetOperatorBodyVisible(engineer, on); break;
                case JobType.Steward: SetOperatorBodyVisible(steward, on); break;
                case JobType.Excavation:
                    _ = excavator;
                    break;
            }
        }
    }

    /// <summary>
    /// Excavator cabin enter/exit — avatar walks to machine, hides while operating, exits beside it.
    /// Machine Transform/sprites always stay; never spawn a second Mara.
    /// </summary>
    public static class ExcavatorCabin
    {
        public const float ExitOffsetX = 0.32f;
        public const float ExitOffsetY = 0.1f;

        public static Vector2 ExitWorld(Vector2 machinePos) =>
            machinePos + new Vector2(ExitOffsetX, ExitOffsetY);

        /// <summary>Enter: presence tracks machine; walking body hidden (inside cabin).</summary>
        public static void Enter(WorkerAvatar av, string providerId)
        {
            if (av == null) return;
            av.SetFollowing(providerId ?? "");
            av.Hide();
        }

        /// <summary>Exit: same avatar appears beside machine; ready to walk.</summary>
        public static void Exit(WorkerAvatar av, Vector2 machineWorld)
        {
            if (av == null) return;
            av.ClearFollowing();
            av.ParkAt(ExitWorld(machineWorld));
            av.Show();
        }
    }
}
