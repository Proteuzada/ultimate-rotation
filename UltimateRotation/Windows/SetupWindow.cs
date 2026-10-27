using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace UltimateRotation.Windows;

internal sealed class SetupWindow : Window
{
    private readonly Configuration configuration;
    private readonly GuideController controller;
    private string clientId = string.Empty;
    private string clientSecret = string.Empty;

    public SetupWindow(Configuration configuration, GuideController controller)
        : base(UiText.SetupTitle)
    {
        this.configuration = configuration;
        this.controller = controller;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(460, 520),
            MaximumSize = new Vector2(900, 1200),
        };
        this.Size = new Vector2(560, 700);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void OnOpen()
    {
        this.clientId = this.configuration.ClientId;
        this.clientSecret = this.configuration.ClientSecret;
    }

    public override void Draw()
    {
        var view = this.controller.Snapshot();
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
        if (ImGui.Button("Load speed rankings"))
        {
            this.PersistCredentials();
            this.controller.SearchRankings();
        }

        ImGui.EndDisabled();

        using (var rankings = ImRaii.Child("rankings", new Vector2(0, 220), true))
        {
            if (rankings.Success)
            {
                if (view.Rankings.Count == 0)
                    ImGui.TextDisabled(job is null ? "Choose a job." : "No rankings loaded.");

                foreach (var row in view.Rankings)
                {
                    var server = string.IsNullOrWhiteSpace(row.Server) ? string.Empty : $" · {row.Server}";
                    var label = $"{row.Rank}. {row.Player}{server} · {row.Code}##{row.Rank}:{row.Code}:{row.FightId}";
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

        var includeOffGcds = view.IncludeOffGcds;
        if (ImGui.Checkbox("Include oGCDs", ref includeOffGcds) && includeOffGcds != view.IncludeOffGcds)
            this.controller.SetIncludeOffGcds(includeOffGcds);

        var autoRotation = view.AutoRotation;
        if (ImGui.Checkbox(UiText.AutoRotation, ref autoRotation) && autoRotation != view.AutoRotation)
            this.controller.SetAutoRotation(autoRotation);
        ImGui.TextDisabled(UiText.AutoRotationHint);

        if (!string.IsNullOrEmpty(view.Error))
            ImGui.TextColored(new Vector4(1f, 0.45f, 0.4f, 1f), view.Error);
        else if (!string.IsNullOrEmpty(view.Status))
            ImGui.TextWrapped(view.Status);

        if (!string.IsNullOrEmpty(view.DutyStatus))
            ImGui.TextWrapped(view.DutyStatus);

        ImGui.TextDisabled("In the selected ultimate, the guide follows the fight from pull start. Leaving the zone or a wipe resets it.");
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
