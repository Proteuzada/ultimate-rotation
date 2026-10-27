using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace UltimateRotation.Windows;

/// <summary>
/// Actions page: icon grid on the left, the selected action's enable switch on the right.
/// </summary>
internal sealed class ActionBrowser
{
    private const float IconSize = 46f;

    private string search = string.Empty;
    private uint selectedId;
    private bool selectedPotion;
    private bool hasSelection;

    public void Draw(GuideController controller, GuideSnapshot view)
    {
        var height = MathF.Max(220f, ImGui.GetContentRegionAvail().Y);
        var width = ImGui.GetContentRegionAvail().X;
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var left = Math.Clamp(width * 0.58f, 240f, width - 220f);

        using (var grid = ImRaii.Child("action-grid", new Vector2(left, height), true))
        {
            if (grid)
                this.DrawGrid(controller, view);
        }

        ImGui.SameLine(0f, gap);

        using var detail = ImRaii.Child("action-detail", new Vector2(0f, height), true);
        if (detail)
            this.DrawDetail(controller, view);
    }

    private void DrawGrid(GuideController controller, GuideSnapshot view)
    {
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##action-search", "Search actions", ref this.search, 80);

        if (ImGui.Button("Use damage"))
            controller.SetDamageActionsEnabled(true);
        ImGui.SameLine();
        if (ImGui.Button("Skip damage"))
            controller.SetDamageActionsEnabled(false);
        if (ImGui.Button("Use mitigations"))
            controller.SetMitigationsEnabled(true);
        ImGui.SameLine();
        if (ImGui.Button("Skip mitigations"))
            controller.SetMitigationsEnabled(false);

        var actions = view.Actions;
        if (actions.Count == 0)
        {
            ImGui.TextDisabled("Choose a job. Damage actions appear after a parse is loaded.");
            return;
        }

        this.DrawGroup(view, actions, "Mitigation", choice => choice.Mitigation, new Vector4(1f, 0.72f, 0.45f, 1f));
        this.DrawGroup(view, actions, "GCD", choice => !choice.Mitigation && !choice.Potion && choice.Kind == StepKind.Gcd, new Vector4(0.92f, 0.92f, 0.92f, 1f));
        this.DrawGroup(view, actions, "oGCD", choice => !choice.Mitigation && !choice.Potion && choice.Kind != StepKind.Gcd, new Vector4(0.65f, 0.82f, 1f, 1f));
        this.DrawGroup(view, actions, "Items", choice => choice.Potion, new Vector4(0.75f, 1f, 0.7f, 1f));
    }

    private void DrawGroup(GuideSnapshot view, IReadOnlyList<ActionChoice> actions, string title, Func<ActionChoice, bool> match, Vector4 color)
    {
        var rows = actions.Where(match).Where(this.MatchesSearch).ToList();
        if (rows.Count == 0)
            return;

        var enabled = rows.Count(choice => choice.Enabled);
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        var open = ImGui.CollapsingHeader($"{title}   {enabled}/{rows.Count}###group-{title}", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.PopStyleColor();
        if (!open)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var columns = Math.Max(1, (int)MathF.Floor((ImGui.GetContentRegionAvail().X + spacing) / (IconSize + spacing)));
        for (var index = 0; index < rows.Count; index++)
        {
            if (index % columns != 0)
                ImGui.SameLine();

            var choice = rows[index];
            var selected = this.IsSelected(choice);
            var id = $"act-{title}-{choice.ActionId}-{(choice.Potion ? 1 : 0)}";
            if (ActionIconButton.Draw(id, IconFor(choice, view.JobSpec), choice.Name, choice.Enabled, selected, IconSize))
                this.Select(choice);

            if (ImGui.IsItemHovered())
            {
                using var tooltip = ImRaii.Tooltip();
                ImGui.Text(choice.Name);
                ImGui.TextDisabled(choice.Enabled ? "Enabled" : "Disabled");
            }
        }
    }

    private void DrawDetail(GuideController controller, GuideSnapshot view)
    {
        var choice = this.Selected(view.Actions);
        if (choice is null)
        {
            ImGui.TextWrapped("Select an action. A check on the icon means automatic rotation may use it. A cross means that step is skipped.");
            return;
        }

        ActionIconButton.DrawImage(IconFor(choice, view.JobSpec), 72f);
        ImGui.Spacing();
        ImGui.Text(choice.Name);
        ImGui.TextDisabled(KindLabel(choice));
        ImGui.Separator();

        var allowed = choice.Enabled;
        if (ImGui.Checkbox($"Enabled##action-{choice.ActionId}-{(choice.Potion ? 1 : 0)}", ref allowed) && allowed != choice.Enabled)
            controller.SetActionEnabled(choice.ActionId, choice.Mitigation, choice.Potion, allowed);

        ImGui.SameLine();
        ImGui.TextDisabled(allowed ? "used" : "skipped");
        ImGui.TextWrapped(Explain(choice));
    }

    private ActionChoice? Selected(IReadOnlyList<ActionChoice> actions)
    {
        if (!this.hasSelection)
            return null;

        foreach (var choice in actions)
        {
            if (this.IsSelected(choice))
                return choice;
        }

        this.hasSelection = false;
        return null;
    }

    private bool IsSelected(ActionChoice choice)
        => this.hasSelection && choice.Potion == this.selectedPotion && choice.ActionId == this.selectedId;

    private void Select(ActionChoice choice)
    {
        this.selectedId = choice.ActionId;
        this.selectedPotion = choice.Potion;
        this.hasSelection = true;
    }

    private bool MatchesSearch(ActionChoice choice)
        => string.IsNullOrWhiteSpace(this.search)
           || choice.Name.Contains(this.search.Trim(), StringComparison.OrdinalIgnoreCase);

    private static uint IconFor(ActionChoice choice, string? jobSpec)
    {
        if (choice.Potion)
            return GameActions.ItemIconId(PotionItem(jobSpec), Plugin.DataManager);

        return GameActions.IconId(choice.ActionId, Plugin.DataManager);
    }

    private static uint PotionItem(string? jobSpec)
    {
        return Gemdraughts.StatFor(null, jobSpec) switch
        {
            Gemdraughts.Dexterity => 49235,
            Gemdraughts.Intelligence => 49237,
            Gemdraughts.Mind => 49238,
            _ => 49234,
        };
    }

    private static string KindLabel(ActionChoice choice)
    {
        if (choice.Potion)
            return "Item";
        if (choice.Mitigation)
            return "Mitigation";
        return choice.Kind == StepKind.Gcd ? "GCD" : "oGCD";
    }

    private static string Explain(ActionChoice choice)
    {
        if (choice.Potion)
            return "On a Gemdraught step, automatic rotation drinks the highest grade in your bags for this job. Off skips that step.";

        if (choice.Mitigation && !choice.InParse)
            return "This mitigation is not in the loaded parse. Enabling it does nothing until a log uses it.";

        if (choice.Mitigation)
            return "Mitigations start off. Turn this on when you want automatic rotation to press it.";

        return choice.Enabled
            ? "This action comes from the loaded parse. Automatic rotation may press it."
            : "This action is skipped. The timeline moves on without pressing it.";
    }
}
