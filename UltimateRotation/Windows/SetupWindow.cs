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
    private string reportInput = string.Empty;

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
        ImGui.Text("ID do cliente");
        ImGui.InputText("##client-id", ref this.clientId, 200);
        ImGui.Text("Segredo do cliente");
        ImGui.InputText("##client-secret", ref this.clientSecret, 200, ImGuiInputTextFlags.Password);

        if (ImGui.Button("Salvar credenciais"))
            this.PersistCredentials();

        ImGui.Separator();
        ImGui.Text("Ultimate");
        var selected = UltimateCatalog.GetOrFirst(view.EncounterId);
        if (ImGui.BeginCombo("##ultimate", selected.Label))
        {
            foreach (var ultimate in UltimateCatalog.All)
            {
                if (ImGui.Selectable(ultimate.Label, ultimate.EncounterId == view.EncounterId))
                    this.controller.SelectUltimate(ultimate.EncounterId);
            }

            ImGui.EndCombo();
        }

        ImGui.BeginDisabled(view.Busy);
        if (ImGui.Button("Buscar rankings de velocidade"))
        {
            this.PersistCredentials();
            this.controller.SearchRankings();
        }

        ImGui.EndDisabled();

        using (var rankings = ImRaii.Child("rankings", new Vector2(0, 140), true))
        {
            if (rankings.Success)
            {
                if (view.Rankings.Count == 0)
                    ImGui.TextDisabled("Nenhum ranking carregado.");

                foreach (var row in view.Rankings)
                {
                    var duration = row.DurationMs is { } milliseconds
                        ? ReportFightInfo.FormatDuration(milliseconds)
                        : "?";
                    var team = string.IsNullOrWhiteSpace(row.Team) ? "Sem time" : row.Team;
                    var server = string.IsNullOrWhiteSpace(row.Server) ? string.Empty : $" · {row.Server}";
                    var label = $"{row.Rank}. {team} · {duration} · {row.Code}{server}";
                    var isSelected = this.reportInput == row.Code && view.PreferredFightId == row.FightId;
                    if (ImGui.Selectable(label, isSelected))
                    {
                        this.reportInput = row.Code;
                        this.controller.PreferFight(row.FightId);
                    }
                }
            }
        }

        ImGui.Text("Ou cole o código ou a URL do relatório");
        ImGui.InputText("##report", ref this.reportInput, 240);
        ImGui.BeginDisabled(view.Busy);
        if (ImGui.Button("Carregar relatório"))
        {
            this.PersistCredentials();
            this.controller.LoadReport(this.reportInput);
        }

        ImGui.EndDisabled();

        ImGui.Separator();
        this.DrawFight(view);
        this.DrawPlayer(view);

        var includeOffGcds = view.IncludeOffGcds;
        if (ImGui.Checkbox("Incluir oGCDs", ref includeOffGcds) && includeOffGcds != view.IncludeOffGcds)
            this.controller.SetIncludeOffGcds(includeOffGcds);

        ImGui.BeginDisabled(view.Busy || view.FightId is null || view.ActorId is null);
        if (ImGui.Button("Montar guia"))
        {
            this.PersistCredentials();
            this.controller.BuildGuide();
        }

        ImGui.EndDisabled();

        if (!string.IsNullOrEmpty(view.Error))
            ImGui.TextColored(new Vector4(1f, 0.45f, 0.4f, 1f), view.Error);
        else if (!string.IsNullOrEmpty(view.Status))
            ImGui.TextWrapped(view.Status);

        ImGui.TextDisabled("O guia avança quando você usa a habilidade, não pelo relógio.");
        ImGui.TextDisabled(UiText.GuideOnly);
    }

    private void DrawFight(GuideSnapshot view)
    {
        ImGui.Text("Pull");
        if (view.Report is null)
        {
            ImGui.TextDisabled("Carregue um relatório.");
            return;
        }

        if (view.Report.Fights.Count == 0)
        {
            ImGui.TextDisabled("Nenhum pull desse ultimate neste relatório.");
            return;
        }

        var current = view.Report.Fights.FirstOrDefault(fight => fight.Id == view.FightId);
        if (ImGui.BeginCombo("##fight", current?.Label ?? "Escolha o pull"))
        {
            foreach (var fight in view.Report.Fights)
            {
                if (ImGui.Selectable(fight.Label, fight.Id == view.FightId))
                    this.controller.SelectFight(fight.Id);
            }

            ImGui.EndCombo();
        }
    }

    private void DrawPlayer(GuideSnapshot view)
    {
        ImGui.Text("Jogador");
        if (view.Report is null || view.FightId is null)
        {
            ImGui.TextDisabled("Escolha um pull.");
            return;
        }

        var fight = view.Report.Fights.FirstOrDefault(item => item.Id == view.FightId);
        if (fight is null)
            return;

        var players = view.Report.PlayersIn(fight);
        var current = players.FirstOrDefault(player => player.Id == view.ActorId);
        if (ImGui.BeginCombo("##player", current?.Label ?? "Escolha o jogador"))
        {
            foreach (var player in players)
            {
                if (ImGui.Selectable(player.Label, player.Id == view.ActorId))
                    this.controller.SelectActor(player.Id);
            }

            ImGui.EndCombo();
        }
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
