using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace UltimateRotation;

internal enum ActionAttempt
{
    Used,
    Blocked,
    Impossible,
}

/// <summary>
/// Presses one action for the local player through ActionManager.UseAction.
/// It never moves the player, never targets another player, and never sends a packet of its own.
/// </summary>
internal static class LocalPlayerAction
{
    public static unsafe ActionAttempt Consider(uint actionId)
    {
        try
        {
            if (actionId == 0)
                return ActionAttempt.Impossible;

            var player = Plugin.ClientState.LocalPlayer;
            var manager = ActionManager.Instance();
            if (player == null || manager == null)
                return ActionAttempt.Blocked;

            var adjusted = manager->GetAdjustedActionId(actionId);
            if (adjusted == 0)
                adjusted = actionId;

            if (!TryResolveTarget(adjusted, player, out var targetId, out var waitingForTarget))
                return waitingForTarget ? ActionAttempt.Blocked : ActionAttempt.Impossible;

            if (manager->GetActionStatus(ActionType.Action, adjusted, targetId) != 0)
                return ActionAttempt.Blocked;

            return manager->UseAction(ActionType.Action, adjusted, targetId)
                ? ActionAttempt.Used
                : ActionAttempt.Blocked;
        }
        catch
        {
            return ActionAttempt.Impossible;
        }
    }

    private static bool TryResolveTarget(uint actionId, IPlayerCharacter player, out ulong targetId, out bool waitingForTarget)
    {
        targetId = player.GameObjectId;
        waitingForTarget = false;
        if (targetId == 0)
            return false;

        if (!GameActions.TryDescribeTarget(actionId, Plugin.DataManager, out var kind))
            return false;

        switch (kind)
        {
            case ActionTargetKind.Ground:
            case ActionTargetKind.OtherPlayer:
                return false;
            case ActionTargetKind.Hostile:
                var current = Plugin.TargetManager.Target;
                if (current == null || current.ObjectKind == ObjectKind.Player || current.GameObjectId == 0)
                {
                    waitingForTarget = true;
                    return false;
                }

                targetId = current.GameObjectId;
                return true;
            default:
                return true;
        }
    }
}
