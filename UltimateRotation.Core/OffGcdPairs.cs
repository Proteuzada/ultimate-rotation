namespace UltimateRotation;

/// <summary>
/// Paired oGCDs that can stand in for each other. The first pair is Machinist's
/// Checkmate and Double Check. A GCD is never a partner. The partner is not used
/// before the step's own time.
/// </summary>
public static class OffGcdPairs
{
    public const uint DoubleCheck = 36979;
    public const uint Checkmate = 36980;

    private static readonly uint[] DoubleCheckIds = [DoubleCheck, 38129, 38851, 40077];
    private static readonly uint[] CheckmateIds = [Checkmate, 38130, 38852, 40075];

    /// <summary>
    /// The other oGCD in the pair, as the base action the game can adjust.
    /// Null when this action has no partner.
    /// </summary>
    public static uint? Partner(uint actionId, uint adjustedActionId, string? name)
    {
        if (Is(actionId, adjustedActionId, name, CheckmateIds, "Checkmate"))
            return DoubleCheck;

        if (Is(actionId, adjustedActionId, name, DoubleCheckIds, "Double Check"))
            return Checkmate;

        return null;
    }

    /// <summary>
    /// The substitute may be pressed only for an oGCD whose step time has arrived.
    /// </summary>
    public static bool MaySwap(StepKind kind, double guideMs, double offsetMs)
        => kind != StepKind.Gcd && guideMs >= offsetMs;

    private static bool Is(uint actionId, uint adjustedActionId, string? name, uint[] ids, string exactName)
    {
        if (Array.IndexOf(ids, actionId) >= 0 || (adjustedActionId != 0 && Array.IndexOf(ids, adjustedActionId) >= 0))
            return true;

        return string.Equals(name, exactName, StringComparison.OrdinalIgnoreCase);
    }
}
