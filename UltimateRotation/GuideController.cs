namespace UltimateRotation;

internal sealed class GuideSnapshot
{
    public bool Busy { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Error { get; init; } = string.Empty;

    public IReadOnlyList<RankedReport> Rankings { get; init; } = [];

    public LoadedReport? Report { get; init; }

    public int EncounterId { get; init; }

    public int? FightId { get; init; }

    public int? ActorId { get; init; }

    public int? PreferredFightId { get; init; }

    public bool IncludeOffGcds { get; init; } = true;

    public GuidePlayback? Playback { get; init; }

    public int Index { get; init; }

    public bool Mismatch { get; init; }

    public string GuideTitle { get; init; } = string.Empty;
}

internal sealed class GuideController : IDisposable
{
    private readonly Configuration configuration;
    private readonly FflogsClient client = new();
    private readonly object gate = new();
    private CancellationTokenSource requests = new();
    private int generation;

    private bool busy;
    private string status = string.Empty;
    private string error = string.Empty;
    private IReadOnlyList<RankedReport> rankings = [];
    private LoadedReport? report;
    private int encounterId;
    private int? fightId;
    private int? actorId;
    private int? preferredFightId;
    private bool includeOffGcds;
    private GuidePlayback? playback;
    private bool mismatch;
    private string guideTitle = string.Empty;
    private TimelineInput? lastInput;
    private TimelineInput? pending;
    private bool announceGuide;

    public GuideController(Configuration configuration)
    {
        this.configuration = configuration;
        this.encounterId = UltimateCatalog.GetOrFirst(configuration.LastEncounterId).EncounterId;
        this.includeOffGcds = configuration.IncludeOffGcds;
    }

    public GuideSnapshot Snapshot()
    {
        lock (this.gate)
        {
            return new GuideSnapshot
            {
                Busy = this.busy,
                Status = this.status,
                Error = this.error,
                Rankings = this.rankings,
                Report = this.report,
                EncounterId = this.encounterId,
                FightId = this.fightId,
                ActorId = this.actorId,
                PreferredFightId = this.preferredFightId,
                IncludeOffGcds = this.includeOffGcds,
                Playback = this.playback,
                Index = this.playback?.Index ?? 0,
                Mismatch = this.mismatch,
                GuideTitle = this.guideTitle,
            };
        }
    }

    public void ForgetToken() => this.client.ForgetToken();

    public void SelectUltimate(int selectedEncounterId)
    {
        lock (this.gate)
        {
            if (this.encounterId == selectedEncounterId)
                return;

            this.encounterId = selectedEncounterId;
            this.rankings = [];
            this.report = null;
            this.fightId = null;
            this.actorId = null;
            this.playback = null;
            this.pending = null;
            this.lastInput = null;
            this.guideTitle = string.Empty;
            this.error = string.Empty;
            this.status = string.Empty;
            this.announceGuide = false;
            this.busy = false;
            this.requests.Cancel();
            this.requests = new CancellationTokenSource();
            this.generation++;
        }

        this.configuration.LastEncounterId = selectedEncounterId;
        this.configuration.Save();
    }

    public void PreferFight(int selectedFightId)
    {
        lock (this.gate)
            this.preferredFightId = selectedFightId;
    }

    public void SelectFight(int selectedFightId)
    {
        lock (this.gate)
        {
            this.fightId = selectedFightId;
            if (this.report is null)
                return;

            var fight = this.report.Fights.FirstOrDefault(item => item.Id == selectedFightId);
            if (fight is null)
                return;

            var players = this.report.PlayersIn(fight);
            if (this.actorId is null || players.All(player => player.Id != this.actorId))
                this.actorId = players.Count == 1 ? players[0].Id : null;
        }
    }

    public void SelectActor(int selectedActorId)
    {
        lock (this.gate)
            this.actorId = selectedActorId;
    }

    public void SetIncludeOffGcds(bool include)
    {
        lock (this.gate)
        {
            this.includeOffGcds = include;
            if (this.lastInput is not null)
                this.pending = this.lastInput;
        }

        this.configuration.IncludeOffGcds = include;
        this.configuration.Save();
    }

    public void SearchRankings()
    {
        var call = this.Begin();
        int encounter;
        lock (this.gate)
            encounter = this.encounterId;

        _ = this.SearchAsync(encounter, call);
    }

    public void LoadReport(string reportText)
    {
        if (!ReportCode.TryExtract(reportText, out var code, out var fightFromUrl))
        {
            lock (this.gate)
            {
                this.error = "Invalid report code.";
                this.status = string.Empty;
            }

            return;
        }

        if (fightFromUrl is not null)
        {
            lock (this.gate)
                this.preferredFightId = fightFromUrl;
        }

        _ = this.LoadReportAsync(code, this.Begin());
    }

    public void BuildGuide() => _ = this.BuildAsync(this.Begin());

    public bool Pump()
    {
        TimelineInput? input;
        var include = true;
        var announce = false;
        lock (this.gate)
        {
            input = this.pending;
            this.pending = null;
            include = this.includeOffGcds;
            announce = this.announceGuide;
            this.announceGuide = false;
        }

        if (input is null)
            return false;

        IReadOnlyList<GuideStep> steps;
        try
        {
            steps = TimelineNormalizer.Normalize(
                input,
                actionId => GameActions.Classify(actionId, Plugin.DataManager, include));
            steps = steps
                .Select(step => step with
                {
                    Name = GameActions.ClientName(step.ActionId, Plugin.DataManager) ?? step.Name,
                    AdjustedActionId = GameActions.Adjusted(step.ActionId),
                })
                .ToList();
        }
        catch (Exception ex)
        {
            lock (this.gate)
            {
                this.error = this.Scrub(ex.Message);
                this.busy = false;
            }

            return false;
        }

        lock (this.gate)
        {
            var title = $"{UltimateCatalog.GetOrFirst(this.encounterId).Alias} · {input.PlayerName}";
            this.playback = new GuidePlayback(steps);
            this.mismatch = false;
            this.guideTitle = title;
            this.busy = false;
            this.error = string.Empty;
            this.status = steps.Count == 0
                ? "The log had no abilities after filtering."
                : $"{steps.Count} steps. The guide advances when you use the ability.";
        }

        return steps.Count > 0 && announce;
    }

    public void NotifyLocalAction(uint actionId, uint adjustedActionId)
    {
        if (AbilityNoise.IsAlwaysDropped(actionId) || AbilityNoise.IsAlwaysDropped(adjustedActionId))
            return;

        lock (this.gate)
        {
            if (this.playback is null || this.playback.IsComplete)
                return;

            this.mismatch = !this.playback.TryAdvance(actionId, adjustedActionId);
        }
    }

    public void Skip()
    {
        lock (this.gate)
        {
            this.playback?.Skip();
            this.mismatch = false;
        }
    }

    public void Back()
    {
        lock (this.gate)
        {
            this.playback?.Back();
            this.mismatch = false;
        }
    }

    public void Reset()
    {
        lock (this.gate)
        {
            this.playback?.Reset();
            this.mismatch = false;
        }
    }

    public void Dispose()
    {
        this.requests.Cancel();
        this.client.Dispose();
        this.requests.Dispose();
    }

    private (int Generation, CancellationToken Token) Begin()
    {
        lock (this.gate)
        {
            this.requests.Cancel();
            this.requests = new CancellationTokenSource();
            this.generation++;
            this.busy = true;
            this.error = string.Empty;
            this.status = UiText.Working;
            return (this.generation, this.requests.Token);
        }
    }

    private async Task SearchAsync(int encounter, (int Generation, CancellationToken Token) call)
    {
        try
        {
            var rows = await this.client.GetSpeedRankingsAsync(
                this.configuration.ClientId,
                this.configuration.ClientSecret,
                encounter,
                call.Token).ConfigureAwait(false);

            lock (this.gate)
            {
                if (call.Generation != this.generation)
                    return;

                this.rankings = rows;
                this.busy = false;
                this.status = rows.Count == 0
                    ? "No speed rankings on this page."
                    : $"{rows.Count} speed rankings.";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            this.Fail(call.Generation, "Could not connect to FFLogs.");
        }
        catch (Exception ex)
        {
            this.Fail(call.Generation, this.Scrub(ex.Message));
        }
    }

    private async Task LoadReportAsync(string code, (int Generation, CancellationToken Token) call)
    {
        try
        {
            var encounter = this.encounterId;
            var loaded = await this.client.GetReportAsync(
                this.configuration.ClientId,
                this.configuration.ClientSecret,
                code,
                encounter,
                call.Token).ConfigureAwait(false);

            lock (this.gate)
            {
                if (call.Generation != this.generation)
                    return;

                this.report = loaded;
                this.playback = null;
                this.pending = null;
                this.lastInput = null;
                this.guideTitle = string.Empty;
                this.fightId = null;
                this.actorId = null;

                var preferred = this.preferredFightId;
                var fight = loaded.Fights.FirstOrDefault(item => item.Id == preferred)
                    ?? loaded.Fights.FirstOrDefault(item => !item.InProgress)
                    ?? loaded.Fights.FirstOrDefault();
                if (fight is not null)
                {
                    this.fightId = fight.Id;
                    var players = loaded.PlayersIn(fight);
                    if (players.Count == 1)
                        this.actorId = players[0].Id;
                }

                this.busy = false;
                this.status = loaded.Fights.Count == 0
                    ? "No pulls of this ultimate in this report."
                    : $"{loaded.Title} · {loaded.Fights.Count} pulls.";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            this.Fail(call.Generation, "Could not connect to FFLogs.");
        }
        catch (Exception ex)
        {
            this.Fail(call.Generation, this.Scrub(ex.Message));
        }
    }

    private async Task BuildAsync((int Generation, CancellationToken Token) call)
    {
        try
        {
            LoadedReport? loaded;
            int? selectedFight;
            int? selectedActor;
            lock (this.gate)
            {
                loaded = this.report;
                selectedFight = this.fightId;
                selectedActor = this.actorId;
            }

            var fight = loaded?.Fights.FirstOrDefault(item => item.Id == selectedFight);
            var actor = loaded?.Actors.FirstOrDefault(item => item.Id == selectedActor);
            if (loaded is null || fight is null || actor is null)
                throw new FflogsException("Choose a pull and a player.");

            var casts = await this.client.GetCastsAsync(
                this.configuration.ClientId,
                this.configuration.ClientSecret,
                loaded,
                fight,
                actor.Id,
                call.Token).ConfigureAwait(false);

            var input = new TimelineInput(
                UltimateCatalog.GetOrFirst(loaded.EncounterId).EnglishName,
                actor.Name,
                loaded.Code,
                fight.Id,
                fight.StartMs,
                fight.EndMs,
                loaded.Phases,
                fight.Transitions,
                casts);

            lock (this.gate)
            {
                if (call.Generation != this.generation)
                    return;

                this.lastInput = input;
                this.pending = input;
                this.announceGuide = true;
                this.status = "Casts received. Building the guide…";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            this.Fail(call.Generation, "Could not connect to FFLogs.");
        }
        catch (Exception ex)
        {
            this.Fail(call.Generation, this.Scrub(ex.Message));
        }
    }

    private void Fail(int callGeneration, string message)
    {
        if (message.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || message.Contains("do not have permission", StringComparison.OrdinalIgnoreCase))
            message = "Private report or missing permission. The public API only reads public reports.";

        if (message.Length > 300)
            message = message[..300];

        lock (this.gate)
        {
            if (callGeneration != this.generation)
                return;

            this.busy = false;
            this.error = message;
            this.status = string.Empty;
        }
    }

    private string Scrub(string message)
    {
        var secret = this.configuration.ClientSecret;
        if (!string.IsNullOrEmpty(secret) && secret.Length >= 8)
            message = message.Replace(secret, "***", StringComparison.Ordinal);

        var clientId = this.configuration.ClientId;
        if (!string.IsNullOrEmpty(clientId) && clientId.Length >= 8)
            message = message.Replace(clientId, "***", StringComparison.Ordinal);

        return message;
    }
}
