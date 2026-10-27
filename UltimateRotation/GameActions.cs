using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaItem = Lumina.Excel.Sheets.Item;

namespace UltimateRotation;

internal enum ActionTargetKind
{
    Self,
    Hostile,
    Ground,
    OtherPlayer,
}

internal static class GameActions
{
    public static AbilityVerdict Classify(uint actionId, IDataManager data, bool includeOffGcds)
    {
        if (AbilityNoise.IsAlwaysDropped(actionId))
            return new AbilityVerdict(true, StepKind.Other);

        var verdict = Lookup(actionId, data);
        return TimelineNormalizer.ApplyOffGcdPreference(verdict, includeOffGcds);
    }

    public static string? ClientName(uint actionId, IDataManager data)
    {
        if (!TryGetAction(actionId, data, out var action))
            return null;

        var name = action.Name.ToString();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    public static bool TryDescribeTarget(uint actionId, IDataManager data, out ActionTargetKind kind)
    {
        kind = ActionTargetKind.Self;
        if (!TryGetAction(actionId, data, out var action))
            return false;

        if (action.TargetArea)
        {
            kind = ActionTargetKind.Ground;
            return true;
        }

        if (action.CanTargetHostile && !action.CanTargetSelf)
        {
            kind = ActionTargetKind.Hostile;
            return true;
        }

        if (action.CanTargetSelf)
        {
            kind = ActionTargetKind.Self;
            return true;
        }

        if (action.CanTargetHostile)
        {
            kind = ActionTargetKind.Hostile;
            return true;
        }

        if (action.CanTargetParty)
        {
            kind = ActionTargetKind.OtherPlayer;
            return true;
        }

        return true;
    }

    public static uint IconId(uint actionId, IDataManager data)
    {
        if (!TryGetAction(actionId, data, out var action))
            return 0;

        return (uint)action.Icon;
    }

    public static uint ItemIconId(uint itemId, IDataManager data)
    {
        try
        {
            var sheet = data.GetExcelSheet<LuminaItem>();
            if (!sheet.TryGetRow(itemId, out var item))
                return 0;

            return (uint)item.Icon;
        }
        catch
        {
            return 0;
        }
    }

    public static unsafe uint Adjusted(uint actionId)
    {
        try
        {
            var manager = ActionManager.Instance();
            if (manager == null)
                return 0;

            var adjusted = manager->GetAdjustedActionId(actionId);
            return adjusted == actionId ? 0 : adjusted;
        }
        catch
        {
            return 0;
        }
    }

    private static AbilityVerdict Lookup(uint actionId, IDataManager data)
    {
        if (!TryGetAction(actionId, data, out var action))
            return new AbilityVerdict(false, StepKind.Other);

        return action.ActionCategory.RowId switch
        {
            1 => new AbilityVerdict(true, StepKind.Other),
            2 or 3 => new AbilityVerdict(false, StepKind.Gcd),
            4 => new AbilityVerdict(false, StepKind.OffGcd),
            8 or 9 or 10 or 11 or 12 or 14 => new AbilityVerdict(true, StepKind.Other),
            _ => new AbilityVerdict(false, StepKind.Other),
        };
    }

    private static bool TryGetAction(uint actionId, IDataManager data, out LuminaAction action)
    {
        action = default;
        try
        {
            var sheet = data.GetExcelSheet<LuminaAction>();
            return sheet.TryGetRow(actionId, out action);
        }
        catch
        {
            return false;
        }
    }
}
