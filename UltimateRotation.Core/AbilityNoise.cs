namespace UltimateRotation;

/// <summary>
/// Actions that are not part of the ability sequence. Damage and mitigation gap closers are not in this list.
/// </summary>
public static class AbilityNoise
{
    public const uint Sprint = 3;
    public const uint AutoAttack = 7;
    public const uint AutoShot = 8;
    public const uint Peloton = 7557;

    public static bool IsAlwaysDropped(uint actionId)
        => actionId is 0 or Sprint or AutoAttack or AutoShot or Peloton;
}
