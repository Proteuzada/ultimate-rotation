using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace UltimateRotation.Windows;

internal enum SettingsPage
{
    Parses,
    Actions,
    Rotation,
}

internal sealed class SetupWindow : Window
{
    private readonly Configuration configuration;
    private readonly GuideController controller;
    private readonly ActionBrowser actions = new();
    private string clientId = string.Empty;
    private string clientSecret = string.Empty;
    private SettingsPage page = SettingsPage.Parses;

    public SetupWindow(Configuration configuration, GuideController controller)
        : base(UiText.SetupTitle)
    {
        this.configuration = configuration;
        this.controller = controller;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(760, 480),
            MaximumSize = new Vector2(1400, 1200),
        };
        this.Size = new Vector2(960, 640);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void ShowPage(SettingsPage target)
    {
        this.page = target;
        this.IsOpen = true;
    }

    public override void OnOpen()
    {
        this.clientId = this.configuration.ClientId;
        this.clientSecret = this.configuration.ClientSecret;
    }

    public override void Draw()
    {
        var view = this.controller.Snapshot();
        var avail = ImGui.GetContentRegionAvail();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var side = Math.Clamp(avail.X * 0.22f, 156f, 210f);

        using (var nav = ImRaii.Child("settings-nav", new Vector2(side, avail.Y), true))
        {
            if (nav)
                this.DrawNav(view);
        }

        var origin = ImGui.GetItemRectMin();
        var navMax = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(navMax.X + (spacing * 0.5f), origin.Y),
            new Vector2(navMax.X + (spacing * 0.5f), origin.Y + avail.Y),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f)));

        ImGui.SameLine(0f, spacing);
        using var body = ImRaii.Child("settings-body", new Vector2(0f, avail.Y), false);
        if (!body)
            return;

        this.DrawHeading(view);
        switch (this.page)
        {
            case SettingsPage.Actions:
                this.actions.Draw(this.controller, view);
                break;
            case SettingsPage.Rotation:
                this.DrawRotation(view);
                break;
            default:
                this.DrawParses(view);
                break;
        }
    }

    private void DrawNav(GuideSnapshot view)
    {
        ImGui.Text("Ultimate Rotation");
        var job = JobCatalog.Find(view.JobSpec)?.EnglishName ?? "No job selected";
        ImGui.TextDisabled(job);
        ImGui.Spacing();
        this.NavItem(UiText.PageParses, SettingsPage.Parses);
        this.NavItem(UiText.PageActions, SettingsPage.Actions);
        this.NavItem(UiText.PageRotation, SettingsPage.Rotation);
    }

    private void NavItem(string label, SettingsPage target)
    {
        var selected = this.page == target;
        if (ImGui.Selectable(label, selected, ImGuiSelectableFlags.None, new Vector2(0f, 32f)))
            this.page = target;

        if (!selected)
            return;

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddRectFilled(
            min,
            new Vector2(min.X + 3f, max.Y),
            ImGui.GetColorU32(new Vector4(0.96f, 0.78f, 0.32f, 1f)));
    }

    private void DrawHeading(GuideSnapshot view)
    {
        var (title, subtitle) = this.page switch
        {
            SettingsPage.Actions => (UiText.PageActions, UiText.ActionsIntro),
            SettingsPage.Rotation => (UiText.PageRotation, UiText.RotationIntro),
            _ => (UiText.PageParses, this.ParseSubtitle(view)),
        };

        ImGui.Text(title);
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.70f, 0.70f, 0.72f, 1f)))
            ImGui.TextWrapped(subtitle);
        ImGui.Separator();
    }

    private string ParseSubtitle(GuideSnapshot view)
    {
        var ultimate = UltimateCatalog.GetOrFirst(view.EncounterId).Label;
        var job = JobCatalog.Find(view.JobSpec)?.EnglishName ?? "choose a job";
        return $"{ultimate} · {job}";
    }

    private void DrawParses(GuideSnapshot view)
    {
        ImGui.TextDisabled(UiText.CredentialsHint);
        ImGui.Text("Client ID");
        ImGui.InputText("##client-id", ref this.clientId, 200);
        ImGui.Text("Client secret");
        ImGui.InputText("##client-secret", ref this.clientSecret, 200, ImGuiInputTextFlags.Password);

        if (ImGui.Button("Save credentials"))
            this.PersistCredentials();

        ImGui.Separator();
        ImGui.Text("Ultimate");
        var selected = UltimateCatalog.GetOrFirst(view.EncounterId);
        if (ImGui.BeginCombo("##ultimate", selected.Label))
        {
            foreach (var ultimate in UltimateCatalog.All)
            {
                if (ImGui.Selectable(ultimate.Label, ultimate.EncounterId == view.EncounterId))
                {
                    this.PersistCredentials();
                    this.controller.SelectUltimate(ultimate.EncounterId);
                }
            }

            ImGui.EndCombo();
        }

        ImGui.Text("Job");
        var job = JobCatalog.Find(view.JobSpec);
        if (ImGui.BeginCombo("##job", job?.EnglishName ?? "Choose a job"))
        {
            foreach (var candidate in JobCatalog.All)
            {
                if (ImGui.Selectable(candidate.EnglishName, candidate.SpecName == view.JobSpec))
                {
                    this.PersistCredentials();
                    this.controller.SelectJob(candidate.SpecName);
                }
            }

            ImGui.EndCombo();
        }

        ImGui.BeginDisabled(view.Busy || job is null);
        if (ImGui.Button("Load parses"))
        {
            this.PersistCredentials();
            this.controller.SearchRankings();
        }

        ImGui.EndDisabled();

        var listHeight = MathF.Max(160f, ImGui.GetContentRegionAvail().Y - 72f);
        using (var rankings = ImRaii.Child("rankings", new Vector2(0, listHeight), true))
        {
            if (rankings.Success)
            {
                if (view.Rankings.Count == 0)
                    ImGui.TextDisabled(job is null ? "Choose a job." : "No parses loaded.");

                var shownParse = int.MinValue;
                foreach (var row in view.Rankings)
                {
                    if (row.Parse != shownParse)
                    {
                        shownParse = row.Parse;
                        ImGui.TextDisabled($"Parse {row.Parse}");
                    }

                    var server = string.IsNullOrWhiteSpace(row.Server) ? string.Empty : $" · {row.Server}";
                    var label = $"{row.Parse} · {row.Player}{server} · {row.Code}##{row.Parse}:{row.Code}:{row.FightId}";
                    var isSelected = view.SelectedCode == row.Code
                        && view.SelectedPlayer == row.Player
                        && view.PreferredFightId == row.FightId;
                    if (ImGui.Selectable(label, isSelected))
                    {
                        this.PersistCredentials();
                        this.controller.OpenRank(row);
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(view.Error))
            ImGui.TextColored(new Vector4(1f, 0.45f, 0.4f, 1f), view.Error);
        else if (!string.IsNullOrEmpty(view.Status))
            ImGui.TextWrapped(view.Status);

        if (!string.IsNullOrEmpty(view.DutyStatus))
            ImGui.TextWrapped(view.DutyStatus);
    }

    private void DrawRotation(GuideSnapshot view)
    {
        var includeOffGcds = view.IncludeOffGcds;
        if (ImGui.Checkbox("Include oGCDs", ref includeOffGcds) && includeOffGcds != view.IncludeOffGcds)
            this.controller.SetIncludeOffGcds(includeOffGcds);

        var autoRotation = view.AutoRotation;
        if (ImGui.Checkbox(UiText.AutoRotation, ref autoRotation) && autoRotation != view.AutoRotation)
            this.controller.SetAutoRotation(autoRotation);
        ImGui.TextDisabled(UiText.AutoRotationHint);

        var startOnCountdown = view.StartOnCountdown;
        if (ImGui.Checkbox(UiText.StartOnCountdown, ref startOnCountdown) && startOnCountdown != view.StartOnCountdown)
            this.controller.SetStartOnCountdown(startOnCountdown);
        ImGui.TextDisabled(UiText.StartOnCountdownHint);

        var useGemdraught = view.UseGemdraught;
        if (ImGui.Checkbox(UiText.UseGemdraught, ref useGemdraught) && useGemdraught != view.UseGemdraught)
            this.controller.SetUseGemdraught(useGemdraught);
        ImGui.TextDisabled(UiText.UseGemdraughtHint);

        if (ImGui.Button("Actions"))
            this.page = SettingsPage.Actions;
        ImGui.SameLine();
        ImGui.TextDisabled("Opens the Actions page in this window.");

        var guideHeight = MathF.Max(180f, ImGui.GetContentRegionAvail().Y);
        using var guide = ImRaii.Child("rotation-guide", new Vector2(0f, guideHeight), true);
        if (guide)
            this.DrawGuide(view);
    }

    private void DrawGuide(GuideSnapshot view)
    {
        var playback = view.Playback;
        if (playback is null)
        {
            ImGui.Text("No guide loaded.");
            ImGui.TextWrapped("Pick an ultimate, a job, and a ranked parse on the Parses page.");
            ImGui.TextDisabled(UiText.GuideOnly);
            return;
        }

        if (!string.IsNullOrEmpty(view.GuideTitle))
            ImGui.TextDisabled(view.GuideTitle);

        if (!string.IsNullOrEmpty(view.DutyStatus))
            ImGui.TextWrapped(view.DutyStatus);

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

    private void PersistCredentials()
    {
        var changed = this.configuration.ClientId != this.clientId.Trim()
            || this.configuration.ClientSecret != this.clientSecret.Trim();
        this.configuration.ClientId = this.clientId.Trim();
        this.configuration.ClientSecret = this.clientSecret.Trim();
        this.configuration.Save();
        if (changed)
            this.controller.ForgetToken();
    }
}
