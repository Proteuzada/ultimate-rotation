using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace UltimateRotation.Windows;

internal sealed class OverlayWindow : Window
{
    private readonly GuideController controller;
    private readonly Action openActions;

    public OverlayWindow(GuideController controller, Action openActions)
        : base(UiText.OverlayTitle, ImGuiWindowFlags.NoScrollbar)
    {
        this.controller = controller;
        this.openActions = openActions;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(280, 180),
            MaximumSize = new Vector2(520, 640),
        };
        this.Size = new Vector2(340, 280);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.RespectCloseHotkey = false;
    }

    public override void Draw()
    {
        var view = this.controller.Snapshot();
        var playback = view.Playback;
        if (playback is null)
        {
            ImGui.Text("No guide loaded.");
            ImGui.TextWrapped("Use /guide to pick an ultimate, a job, and a ranked parse.");
            ImGui.TextDisabled(UiText.GuideOnly);
            return;
        }

        if (!string.IsNullOrEmpty(view.GuideTitle))
            ImGui.TextDisabled(view.GuideTitle);

        if (!string.IsNullOrEmpty(view.DutyStatus))
            ImGui.TextWrapped(view.DutyStatus);

        var autoRotation = view.AutoRotation;
        if (ImGui.Checkbox(UiText.AutoRotation, ref autoRotation) && autoRotation != view.AutoRotation)
            this.controller.SetAutoRotation(autoRotation);
        ImGui.TextDisabled(UiText.AutoRotationHint);

        var useGemdraught = view.UseGemdraught;
        if (ImGui.Checkbox(UiText.UseGemdraught, ref useGemdraught) && useGemdraught != view.UseGemdraught)
            this.controller.SetUseGemdraught(useGemdraught);
        ImGui.TextDisabled(UiText.UseGemdraughtHint);
        if (ImGui.Button("Actions"))
            this.openActions();

        var index = view.Index;
        var phase = index < playback.Steps.Count
            ? playback.Steps[index].PhaseName
            : playback.PhaseName;
        ImGui.Text($"Phase: {(string.IsNullOrEmpty(phase) ? "Pull" : phase)}");

        if (playback.Steps.Count == 0)
        {
            ImGui.Text("No steps left after filtering.");
            ImGui.TextDisabled(UiText.GuideOnly);
            return;
        }

        if (index >= playback.Steps.Count)
        {
            ImGui.Text("Sequence complete.");
        }
        else
        {
            ImGui.Text($"Step {index + 1} of {playback.Steps.Count}");
            DrawStep(playback.Steps[index], current: true);
            ImGui.Spacing();
            ImGui.Text("Next");
            var shownPhase = playback.Steps[index].PhaseName;
            var last = Math.Min(playback.Steps.Count, index + 6);
            for (var i = index + 1; i < last; i++)
            {
                if (playback.Steps[i].PhaseName != shownPhase)
                {
                    shownPhase = playback.Steps[i].PhaseName;
                    ImGui.TextDisabled(shownPhase);
                }

                DrawStep(playback.Steps[i], current: false);
            }
        }

        if (view.Mismatch)
            ImGui.TextDisabled("The ability you used is not the current step.");

        if (ImGui.Button("Skip"))
            this.controller.Skip();
        ImGui.SameLine();
        if (ImGui.Button("Back"))
            this.controller.Back();
        ImGui.SameLine();
        if (ImGui.Button("Restart"))
            this.controller.Reset();

        ImGui.TextDisabled(UiText.GuideOnly);
    }

    private static void DrawStep(GuideStep step, bool current)
    {
        var color = step.Kind switch
        {
            StepKind.Gcd => new Vector4(1f, 1f, 1f, 1f),
            StepKind.OffGcd => new Vector4(0.65f, 0.82f, 1f, 1f),
            _ => new Vector4(1f, 0.9f, 0.55f, 1f),
        };
        var prefix = current ? "► " : "   ";
        ImGui.TextColored(color, $"{prefix}{step.Name}  {UiText.Kind(step.Kind)}");
    }
}
