namespace UltimateRotation;

public sealed record LogAction(uint ActionId, string Name, StepKind Kind);

public sealed record ActionChoice(uint ActionId, string Name, StepKind Kind, bool Mitigation, bool Potion, bool Enabled);

/// <summary>
/// The Actions menu. Mitigations start disabled. Every other action from the parse starts enabled.
/// </summary>
public static class ActionMenu
{
    public static bool Allows(
        uint actionId,
        string? name,
        bool useGemdraught,
        IReadOnlySet<uint> disabled,
        IReadOnlySet<uint> enabledMitigations)
    {
        if (Gemdraughts.IsStep(name))
            return useGemdraught;

        if (disabled.Contains(actionId))
            return false;

        if (MitigationCatalog.IsMitigation(actionId, name))
            return enabledMitigations.Contains(actionId);

        return true;
    }

    public static IReadOnlyList<ActionChoice> Build(
        string? specName,
        IReadOnlyList<LogAction> logActions,
        bool sawGemdraught,
        bool useGemdraught,
        IReadOnlySet<uint> disabled,
        IReadOnlySet<uint> enabledMitigations)
    {
        var byId = new Dictionary<uint, ActionChoice>();

        foreach (var mitigation in MitigationCatalog.ForJob(specName))
        {
            byId[mitigation.ActionId] = new ActionChoice(
                mitigation.ActionId,
                mitigation.Name,
                StepKind.OffGcd,
                true,
                false,
                Allows(mitigation.ActionId, mitigation.Name, useGemdraught, disabled, enabledMitigations));
        }

        foreach (var action in logActions)
        {
            if (Gemdraughts.IsStep(action.Name))
                continue;

            var mitigation = MitigationCatalog.IsMitigation(action.ActionId, action.Name);
            byId[action.ActionId] = new ActionChoice(
                action.ActionId,
                action.Name,
                action.Kind,
                mitigation,
                false,
                Allows(action.ActionId, action.Name, useGemdraught, disabled, enabledMitigations));
        }

        var choices = byId.Values.ToList();
        if (sawGemdraught)
        {
            choices.Add(new ActionChoice(0, "Gemdraught", StepKind.OffGcd, false, true, useGemdraught));
        }

        return choices
            .OrderBy(choice => Group(choice))
            .ThenBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static int Group(ActionChoice choice)
    {
        if (choice.Mitigation)
            return 0;
        if (choice.Potion)
            return 3;
        return choice.Kind == StepKind.Gcd ? 1 : 2;
    }
}
