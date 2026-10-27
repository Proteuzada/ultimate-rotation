using Dalamud.Game.ClientState.Objects.Types;

namespace UltimateRotation;

/// <summary>
/// A striking dummy is a battle NPC named as one, or the long-standing dummy name id 541.
/// </summary>
internal static class StrikingDummy
{
    public static bool IsCurrentTarget()
    {
        var target = Plugin.TargetManager.Target;
        if (target is not IBattleChara battle)
            return false;

        if (battle.NameId == 541)
            return true;

        var name = battle.Name.TextValue;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return name.Contains("Striking Dummy", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Mannequin d'entraînement", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Übungsattrappe", StringComparison.OrdinalIgnoreCase)
            || name.Contains("木人", StringComparison.Ordinal);
    }
}
