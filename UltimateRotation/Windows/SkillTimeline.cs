using Dalamud.Bindings.ImGui;

namespace UltimateRotation.Windows;

/// <summary>
/// Icon column beside the text steps. The highlighted row is the step the guide is on,
/// which is also the step automatic rotation is pressing.
/// </summary>
internal sealed class SkillTimeline
{
    private int followedIndex = -1;

    public void Draw(GuidePlayback playback, int currentIndex, string? jobSpec)
    {
        var bringCurrent = this.followedIndex != currentIndex;
        var shownPhase = string.Empty;
        for (var i = 0; i < playback.Steps.Count; i++)
        {
            var step = playback.Steps[i];
            if (step.PhaseName != shownPhase)
            {
                shownPhase = step.PhaseName;
                ImGui.TextDisabled(string.IsNullOrEmpty(shownPhase) ? "Pull" : shownPhase);
            }

            var current = i == currentIndex;
            ActionIconButton.DrawSkillRow(
                $"skill-step-{step.Index}",
                IconFor(step, jobSpec),
                step.Name,
                current,
                past: currentIndex < playback.Steps.Count && i < currentIndex,
                bringIntoView: bringCurrent && current);
        }

        this.followedIndex = currentIndex;
    }

    private static uint IconFor(GuideStep step, string? jobSpec)
    {
        if (Gemdraughts.IsStep(step.Name))
            return GameActions.ItemIconId(PotionItem(step.Name, jobSpec), Plugin.DataManager);

        var actionId = step.AdjustedActionId != 0 ? step.AdjustedActionId : step.ActionId;
        var icon = GameActions.IconId(actionId, Plugin.DataManager);
        return icon != 0 || actionId == step.ActionId
            ? icon
            : GameActions.IconId(step.ActionId, Plugin.DataManager);
    }

    private static uint PotionItem(string name, string? jobSpec)
    {
        return Gemdraughts.StatFor(name, jobSpec) switch
        {
            Gemdraughts.Dexterity => 49235,
            Gemdraughts.Intelligence => 49237,
            Gemdraughts.Mind => 49238,
            _ => 49234,
        };
    }
}
