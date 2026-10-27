using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace UltimateRotation.Windows;

internal sealed class OverlayWindow : Window
{
    private readonly GuideController controller;

    public OverlayWindow(GuideController controller)
        : base(UiText.OverlayTitle, ImGuiWindowFlags.NoScrollbar)
    {
        this.controller = controller;
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
            ImGui.Text("Nenhum guia carregado.");
            ImGui.TextWrapped("Use /guia para escolher um ultimate, um relatório, um pull e um jogador.");
            ImGui.TextDisabled(UiText.GuideOnly);
            return;
        }

        if (!string.IsNullOrEmpty(view.GuideTitle))
            ImGui.TextDisabled(view.GuideTitle);

        var index = view.Index;
        var phase = index < playback.Steps.Count
            ? playback.Steps[index].PhaseName
            : playback.PhaseName;
        ImGui.Text($"Fase: {(string.IsNullOrEmpty(phase) ? "Pull" : phase)}");

        if (playback.Steps.Count == 0)
        {
            ImGui.Text("Nenhum passo depois do filtro.");
            ImGui.TextDisabled(UiText.GuideOnly);
            return;
        }

        if (index >= playback.Steps.Count)
        {
            ImGui.Text("Sequência concluída.");
        }
        else
        {
            ImGui.Text($"Passo {index + 1} de {playback.Steps.Count}");
            DrawStep(playback.Steps[index], current: true);
            ImGui.Spacing();
            ImGui.Text("Depois");
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
            ImGui.TextDisabled("A habilidade usada não é o passo atual.");

        if (ImGui.Button("Pular"))
            this.controller.Skip();
        ImGui.SameLine();
        if (ImGui.Button("Voltar"))
            this.controller.Back();
        ImGui.SameLine();
        if (ImGui.Button("Reiniciar"))
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
