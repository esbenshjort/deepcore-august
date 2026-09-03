#if GEO_DIAG_CONSOLE
namespace DeepCore.FreeMovement
{
    /// <summary>Stub so WorkerStatProfiles compiles outside Unity without excavator harness.</summary>
    public enum ExcavatorTestProfile : byte
    {
        Baseline = 0,
        Brute = 1,
        Technician = 2,
        Professional = 3,
        Cowboy = 4,
        Ace = 5,
        Green = 6,
    }

    public static class ExcavatorBalanceHarness
    {
        public static WorkerStats BuildProfile(ExcavatorTestProfile profile) =>
            WorkerStats.CreateBaseline();
    }
}
#endif
