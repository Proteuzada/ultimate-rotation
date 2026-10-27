using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace UltimateRotation.Windows;

internal sealed class ActionsWindow : Window
{
    private readonly GuideController controller;
    private string search = string.Empty;

    public ActionsWindow(GuideController controller)
        : base(UiText.ActionsTitle)
    {
        this.controller = controller;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380, 420),
            MaximumSize = new Vector2(720, 900),
        };
        this.Size = new Vector2(440, 560);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var view = this.controller.Snapshot();
        var job = JobCatalog.Find(view.JobSpec)?.EnglishName ?? "Choose a job";
        ImGui.Text($"Job: {job}");
        ImGui.TextWrapped(UiText.ActionsHint);
        ImGui.InputText("Search", ref this.search, 80);

        if (ImGui.Button("Use damage actions"))
            this.controller.SetDamageActionsEnabled(true);
        ImGui.SameLine();
        if (ImGui.Button("Skip damage actions"))
            this.controller.SetDamageActionsEnabled(false);
        if (ImGui.Button("Use mitigations"))
            this.controller.SetMitigationsEnabled(true);
        ImGui.SameLine();
        if (ImGui.Button("Skip mitigations"))
            this.controller.SetMitigationsEnabled(false);

        ImGui.Separator();

        var actions = view.Actions;
        if (actions.Count == 0)
        {
            ImGui.TextDisabled("Choose a job. Damage actions appear after a parse is loaded.");
            return;
        }

        using var child = ImRaii.Child("action-list", new Vector2(0, 0), true);
        if (!child.Success)
            return;

        this.DrawGroup(actions, "Mitigation", choice => choice.Mitigation, new Vector4(1f, 0.72f, 0.45f, 1f));
        this.DrawGroup(actions, "GCD", choice => !choice.Mitigation && !choice.Potion && choice.Kind == StepKind.Gcd, new Vector4(1f, 1f, 1f, 1f));
        this.DrawGroup(actions, "oGCD", choice => !choice.Mitigation && !choice.Potion && choice.Kind != StepKind.Gcd, new Vector4(0.65f, 0.82f, 1f, 1f));
        this.DrawGroup(actions, "Potion", choice => choice.Potion, new Vector4(0.75f, 1f, 0.7f, 1f));
    }

    private void DrawGroup(IReadOnlyList<ActionChoice> actions, string title, Func<ActionChoice, bool> match, Vector4 color)
    {
        var rows = actions.Where(match).Where(this.MatchesSearch).ToList();
        if (rows.Count == 0)
            return;

        var enabled = rows.Count(choice => choice.Enabled);
        if (!ImGui.CollapsingHeader($"{title}  {enabled}/{rows.Count}###group-{title}", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        foreach (var choice in rows)
        {
            var allowed = choice.Enabled;
            if (ImGui.Checkbox($"##use-{title}-{choice.ActionId}-{choice.Name}", ref allowed) && allowed != choice.Enabled)
                this.controller.SetActionEnabled(choice.ActionId, choice.Mitigation, choice.Potion, allowed);

            ImGui.SameLine();
            ImGui.TextColored(color, choice.Name);
            ImGui.SameLine();
            ImGui.TextDisabled(allowed ? "used" : "skipped");
        }
    }

    private bool MatchesSearch(ActionChoice choice)
        => string.IsNullOrWhiteSpace(this.search)
           || choice.Name.Contains(this.search.Trim(), StringComparison.OrdinalIgnoreCase);
}
