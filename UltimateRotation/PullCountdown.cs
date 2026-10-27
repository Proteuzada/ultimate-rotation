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
            if (agent == null || !agent->ShowingCountdown)
                return false;

            remainingSeconds = agent->TimeRemaining;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
