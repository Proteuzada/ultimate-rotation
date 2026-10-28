using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace UltimateRotation;

internal enum ActionAttempt
{
    Used,
    Queued,
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

            var player = Plugin.Objects.LocalPlayer;
            var manager = ActionManager.Instance();
            if (player == null || manager == null)
                return ActionAttempt.Blocked;

            var adjusted = manager->GetAdjustedActionId(actionId);
            if (adjusted == 0)
                adjusted = actionId;

            if (!TryResolveTarget(adjusted, player, out var targetId, out var waitingForTarget))
                return waitingForTarget ? ActionAttempt.Blocked : ActionAttempt.Impossible;

            // The game keeps one queued press. A new UseAction would replace it.
            if (manager->ActionQueued)
                return ActionAttempt.Blocked;

            if (manager->GetActionStatus(ActionType.Action, adjusted, targetId) != 0
                && !CanQueue(manager, adjusted))
                return ActionAttempt.Blocked;

            if (!manager->UseAction(ActionType.Action, adjusted, targetId))
                return ActionAttempt.Blocked;

            return IsQueued(manager, adjusted) ? ActionAttempt.Queued : ActionAttempt.Used;
        }
        catch
        {
            return ActionAttempt.Impossible;
        }
    }

    public static unsafe bool SlotOccupied()
    {
        try
        {
            var manager = ActionManager.Instance();
            return manager != null && manager->ActionQueued;
        }
        catch
        {
            return false;
        }
    }

    public const int GcdRecastGroup = 58;

    /// <summary>
    /// Remaining time on the weaponskill GCD, in milliseconds. This is the same recast
    /// a rotation uses to decide the weave window, not an oGCD's own cooldown.
    /// </summary>
    public static unsafe double WeaponRemainMs()
    {
        try
        {
            var manager = ActionManager.Instance();
            if (manager == null)
                return 0;

            var detail = manager->GetRecastGroupDetail(GcdRecastGroup);
            if (detail == null || !detail->IsActive)
                return 0;

            return Math.Max(0, detail->Total - detail->Elapsed) * 1000d;
        }
        catch
        {
            return 0;
        }
    }

    public static unsafe double AnimationLockMs()
    {
        try
        {
            var manager = ActionManager.Instance();
            return manager == null ? 0 : Math.Max(0, manager->AnimationLock) * 1000d;
        }
        catch
        {
            return 0;
        }
    }

    public static unsafe double RecastRemainingMs(uint actionId)
    {
        try
        {
            var manager = ActionManager.Instance();
            if (manager == null || actionId == 0)
                return 0;

            var adjusted = manager->GetAdjustedActionId(actionId);
            if (adjusted == 0)
                adjusted = actionId;

            var recast = manager->GetRecastTime(ActionType.Action, adjusted);
            var elapsed = manager->GetRecastTimeElapsed(ActionType.Action, adjusted);
            return Math.Max(0, recast - elapsed) * 1000d;
        }
        catch
        {
            return 0;
        }
    }

    public static unsafe double RemainingLockMs(uint actionId)
    {
        try
        {
            var manager = ActionManager.Instance();
            if (manager == null || actionId == 0)
                return 0;

            var adjusted = manager->GetAdjustedActionId(actionId);
            if (adjusted == 0)
                adjusted = actionId;

            var animation = Math.Max(0, manager->AnimationLock);
            var recast = manager->GetRecastTime(ActionType.Action, adjusted);
            var elapsed = manager->GetRecastTimeElapsed(ActionType.Action, adjusted);
            var cooldown = Math.Max(0, recast - elapsed);
            return Math.Max(animation, cooldown) * 1000d;
        }
        catch
        {
            return 0;
        }
    }

    public static unsafe bool IsQueued(uint actionId)
    {
        try
        {
            var manager = ActionManager.Instance();
            if (manager == null)
                return false;

            var adjusted = actionId == 0 ? 0 : manager->GetAdjustedActionId(actionId);
            if (adjusted == 0)
                adjusted = actionId;
            return IsQueued(manager, adjusted) || IsQueued(manager, actionId);
        }
        catch
        {
            return false;
        }
    }

    private static unsafe bool CanQueue(ActionManager* manager, uint actionId)
    {
        if (manager->ActionQueued)
            return false;

        var animation = Math.Max(0, manager->AnimationLock);
        var recast = manager->GetRecastTime(ActionType.Action, actionId);
        var elapsed = manager->GetRecastTimeElapsed(ActionType.Action, actionId);
        var cooldown = Math.Max(0, recast - elapsed);
        var remaining = Math.Max(animation, cooldown);
        return remaining > 0 && remaining <= ButtonQueue.WindowMs / 1000d;
    }

    private static unsafe bool IsQueued(ActionManager* manager, uint actionId)
        => actionId != 0 && manager->ActionQueued && manager->QueuedActionId == actionId;

    private static bool TryResolveTarget(uint actionId, IGameObject player, out ulong targetId, out bool waitingForTarget)
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
                if (current == null || current.ObjectKind == ObjectKind.Pc || current.GameObjectId == 0)
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
