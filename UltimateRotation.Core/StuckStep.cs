namespace UltimateRotation;

public enum StepAvailability
{
    Press,
    Hold,
    Skip,
}

/// <summary>
/// A timeline step that cannot become ready is skipped. A recast, a cast, or a rejection
/// this instant is held and pressed again, because UseAction would not keep it.
/// An empty gauge or a missing resource is never that wait.
/// </summary>
public static class StuckStep
{
    public const uint NotEnoughHp = 567;
    public const uint NotEnoughMp = 568;
    public const uint NotEnoughGp = 570;
    public const uint NotEnoughCp = 571;
    public const uint RequirementNotMet = 572;
    public const uint NotLearned = 573;
    public const uint WrongClass = 574;
    public const uint StatusAffliction = 581;
    public const uint OnCooldown = 582;
    public const uint NoItem = 583;
    public const uint NotEnoughEp = 5205;
    public const uint NoDutyActionCharges = 5209;

    public static bool IsGaugeOrResource(uint status) => status is NotEnoughHp or NotEnoughMp or NotEnoughGp
        or NotEnoughCp or RequirementNotMet or NotEnoughEp;

    public const uint Casting = 580;
    public const uint CannotExecute = 579;

    public static StepAvailability Classify(uint status, double recastRemainMs, double animationLockMs, bool casting)
    {
        if (casting)
            return StepAvailability.Hold;

        if (IsGaugeOrResource(status) || IsHardImpediment(status))
            return StepAvailability.Skip;

        // On cooldown, still casting, or not executable this instant. UseAction would
        // reject the press, so the step is held and tried again. It is not dropped.
        if (status != 0 || NonNegative(recastRemainMs) > 0 || NonNegative(animationLockMs) > 0)
            return StepAvailability.Hold;

        return StepAvailability.Press;
    }

    /// <summary>
    /// The step is skipped when the logged action cannot become ready and its partner,
    /// if any, cannot either. A short recast on either action keeps the hold.
    /// </summary>
    public static bool ShouldSkip(StepAvailability logged, StepAvailability? partner)
    {
        if (logged == StepAvailability.Press || logged == StepAvailability.Hold)
            return false;

        return partner is not StepAvailability.Press and not StepAvailability.Hold;
    }

    private static bool IsHardImpediment(uint status) => status is NotLearned or WrongClass or StatusAffliction
        or NoItem or NoDutyActionCharges;

    private static double NonNegative(double value)
        => double.IsNaN(value) || value < 0 ? 0 : value;
}
