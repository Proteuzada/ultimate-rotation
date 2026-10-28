using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace UltimateRotation.Windows;

/// <summary>
/// The skill-icon timeline in its own window. It stays open when the settings window is closed,
/// and the highlighted row is still the step automatic rotation is on.
/// </summary>
internal sealed class SkillTimelineWindow : Window
{
    private readonly GuideController controller;
    private readonly SkillTimeline skills = new();

    public SkillTimelineWindow(GuideController controller)
        : base(UiText.SkillsWindow)
    {
        this.controller = controller;
        this.ForceMainWindow = true;
        this.RespectCloseHotkey = false;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(220, 180),
            MaximumSize = new Vector2(480, 1200),
        };
        this.Size = new Vector2(280, 520);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.Position = new Vector2(80, 80);
        this.PositionCondition = ImGuiCond.FirstUseEver;
    }

    public override void OnClose()
    {
        if (!this.controller.Snapshot().ShowSkills)
            return;

        this.controller.SetSkillsDetached(false);
    }

    public override void Draw()
    {
        var view = this.controller.Snapshot();
        if (ImGui.SmallButton(UiText.Attach))
        {
            this.controller.SetSkillsDetached(false);
            this.IsOpen = false;
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(UiText.AttachHint);

        var playback = view.Playback;
        if (playback is null || playback.Steps.Count == 0)
        {
            ImGui.TextDisabled("No guide loaded.");
            return;
        }

        var height = MathF.Max(80f, ImGui.GetContentRegionAvail().Y);
        using var list = ImRaii.Child("detached-skill-list", new Vector2(0f, height), false);
        if (list)
            this.skills.Draw(playback, view.Index, view.JobSpec);
    }
}
