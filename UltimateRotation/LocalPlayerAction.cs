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
    private static uint expectingId;
    private static uint expectingAdjusted;

    /// <summary>The id passed to the last UseAction, kept after the call returns.</summary>
    public static uint LastSentId { get; private set; }

    /// <summary>The action id of the UseAction call that is on the stack, if we made it.</summary>
    public static void Expect(uint actionId, uint adjusted)
    {
        expectingId = actionId;
        expectingAdjusted = adjusted == 0 ? actionId : adjusted;
        LastSentId = expectingAdjusted;
    }

    public static void ClearExpect()
    {
        expectingId = 0;
        expectingAdjusted = 0;
    }

    public static bool IsExpected(uint actionId, uint adjusted)
        => MatchesExpected(actionId) || MatchesExpected(adjusted);

    private static bool MatchesExpected(uint id)
        => id != 0 && (id == expectingId || id == expectingAdjusted);

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

            // The slot holds one press. Replacing it drops whichever action was stored.
            if (manager->ActionQueued)
            {
                if (!IsQueued(manager, adjusted) && !IsQueued(manager, actionId))
                    return ActionAttempt.Blocked;

                var queuedId = manager->QueuedActionId;
                LastSentId = queuedId != 0 ? queuedId : adjusted;
                return ActionAttempt.Queued;
            }

            // UseAction is rejected while the animation lock or a cast is still running.
            if (manager->AnimationLock > 0.001f)
                return ActionAttempt.Blocked;

            if (player.TotalCastTime > 0.05f && player.CurrentCastTime + 0.05f < player.TotalCastTime)
                return ActionAttempt.Blocked;

            var status = manager->GetActionStatus(ActionType.Action, adjusted, targetId);
            if (status != 0 && !InGameQueueWindow(manager, adjusted))
                return ActionAttempt.Blocked;

            Expect(actionId, adjusted);
            bool fired;
            try
            {
                fired = manager->UseAction(ActionType.Action, adjusted, targetId);
            }
            finally
            {
                ClearExpect();
            }

            if (!fired)
                return ActionAttempt.Blocked;

            // ActionQueued means the game stored the press. That is not the cast.
            if (manager->ActionQueued)
            {
                var queuedId = manager->QueuedActionId;
                if (queuedId != 0)
                    LastSentId = queuedId;
                return ActionAttempt.Queued;
            }

            return ActionAttempt.Used;
        }
        catch
        {
            return ActionAttempt.Blocked;
        }
    }

    /// <summary>
    /// Looks at the action without pressing it. An empty gauge or a missing resource is
    /// <see cref="StepAvailability.Skip"/>. A recast, a cast, or the animation lock is
    /// <see cref="StepAvailability.Hold"/> and the press is tried again.
    /// </summary>
    public static unsafe StepAvailability Availability(uint actionId)
    {
        try
        {
            if (actionId == 0)
                return StepAvailability.Skip;

            var player = Plugin.Objects.LocalPlayer;
            var manager = ActionManager.Instance();
            if (player == null || manager == null)
                return StepAvailability.Hold;

            var adjusted = manager->GetAdjustedActionId(actionId);
            if (adjusted == 0)
                adjusted = actionId;

            if (!TryResolveTarget(adjusted, player, out var targetId, out var waitingForTarget))
                return waitingForTarget ? StepAvailability.Hold : StepAvailability.Skip;

            if (manager->ActionQueued)
                return StepAvailability.Hold;

            var status = manager->GetActionStatus(ActionType.Action, adjusted, targetId);
            if (!StuckStep.IsGaugeOrResource(status) && manager->GetCurrentCharges(adjusted) > 0)
                status = 0;

            var casting = player.TotalCastTime > 0.05f && player.CurrentCastTime + 0.05f < player.TotalCastTime;
            return StuckStep.Classify(status, RecastRemainingMs(actionId), AnimationLockMs(), casting);
        }
        catch
        {
            return StepAvailability.Hold;
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

    /// <summary>
    /// The game accepts one queued GCD during the last half-second of its recast,
    /// after the animation lock has already cleared. An oGCD is not given that wait.
    /// </summary>
    private static unsafe bool InGameQueueWindow(ActionManager* manager, uint actionId)
    {
        var recast = manager->GetRecastTime(ActionType.Action, actionId);
        var elapsed = manager->GetRecastTimeElapsed(ActionType.Action, actionId);
        var remain = Math.Max(0, recast - elapsed);
        return remain > 0 && remain <= ButtonQueue.WindowMs / 1000d;
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
