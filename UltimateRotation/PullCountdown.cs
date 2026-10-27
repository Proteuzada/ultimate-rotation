using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace UltimateRotation;

/// <summary>
/// The duty countdown shown before a pull. Remaining time is seconds left on that countdown.
/// </summary>
internal static class PullCountdown
{
    public static unsafe bool TryRead(out float remainingSeconds)
    {
        remainingSeconds = 0;
        try
        {
            var agent = AgentCountDownSettingDialog.Instance();
            if (agent == null)
                return false;

            // Active is the countdown itself. ShowingCountdown can already be false
            // while the 1 is still the number the player sees.
            var remaining = agent->TimeRemaining;
            if (remaining <= 0f || remaining > 30f)
                return false;

            if (!agent->Active && !agent->ShowingCountdown)
                return false;

            remainingSeconds = remaining;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
