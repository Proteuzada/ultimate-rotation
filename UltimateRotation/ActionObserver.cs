using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace UltimateRotation;

/// <summary>
/// Watches the local player's own UseAction. The detour never changes the call and never invokes UseAction itself.
/// </summary>
internal sealed class ActionObserver : IDisposable
{
    private readonly GuideController controller;
    private readonly IPluginLog log;
    private Hook<ActionManager.Delegates.UseAction>? hook;

    public unsafe ActionObserver(GuideController controller, IGameInteropProvider interop, IPluginLog log)
    {
        this.controller = controller;
        this.log = log;

        try
        {
            var address = (nint)ActionManager.MemberFunctionPointers.UseAction;
            if (address == 0)
            {
                this.log.Warning("UseAction is unavailable. The guide still follows the pull in the duty, and Skip still works outside it.");
                return;
            }

            this.hook = interop.HookFromAddress<ActionManager.Delegates.UseAction>(address, this.OnUseAction);
            this.hook.Enable();
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Could not watch abilities. Manual skip still works.");
        }
    }

    public void Dispose() => this.hook?.Dispose();

    private unsafe bool OnUseAction(
        ActionManager* manager,
        ActionType actionType,
        uint actionId,
        ulong targetId,
        uint extraParam,
        ActionManager.UseActionMode mode,
        uint comboRouteId,
        bool* outOptAreaTargeted)
    {
        var executed = this.hook!.Original(
            manager,
            actionType,
            actionId,
            targetId,
            extraParam,
            mode,
            comboRouteId,
            outOptAreaTargeted);

        if (!executed || actionType != ActionType.Action)
            return executed;

        // Storing the press is not the cast. The game delivers the cast with UseActionMode.Queue.
        if (mode != ActionManager.UseActionMode.Queue && manager->ActionQueued)
        {
            var queued = manager->QueuedActionId;
            if (queued == actionId || LocalPlayerAction.IsExpected(actionId, queued))
                return executed;
        }

        var adjusted = actionId;
        try
        {
            var resolved = manager->GetAdjustedActionId(actionId);
            if (resolved != 0)
                adjusted = resolved;
        }
        catch
        {
            adjusted = actionId;
        }

        this.controller.NotifyLocalAction(actionId, adjusted);
        return executed;
    }
}
