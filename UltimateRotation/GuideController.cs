namespace UltimateRotation;

internal sealed class GuideSnapshot
{
    public bool Busy { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Error { get; init; } = string.Empty;

    public IReadOnlyList<RankedReport> Rankings { get; init; } = [];

    public LoadedReport? Report { get; init; }

    public int EncounterId { get; init; }

    public string? JobSpec { get; init; }

    public string? SelectedPlayer { get; init; }

    public string? SelectedCode { get; init; }

    public int? FightId { get; init; }

    public int? ActorId { get; init; }

    public int? PreferredFightId { get; init; }

    public bool IncludeOffGcds { get; init; } = true;

    public GuidePlayback? Playback { get; init; }

    public int Index { get; init; }

    public bool Mismatch { get; init; }

    public string GuideTitle { get; init; } = string.Empty;

    public string DutyStatus { get; init; } = string.Empty;
}

internal enum DutySync
{
    None,
    PullStarted,
    Reset,
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
    private string? jobSpec;
    private string? rankedPlayer;
    private string? rankedCode;
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
    private bool inDutyZone;
    private bool pullActive;
    private DateTime? pullStartedUtc;
    private string dutyStatus = string.Empty;

    public GuideController(Configuration configuration)
    {
        this.configuration = configuration;
        this.encounterId = UltimateCatalog.GetOrFirst(configuration.LastEncounterId).EncounterId;
        this.jobSpec = JobCatalog.Find(configuration.LastJobSpec)?.SpecName;
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
                JobSpec = this.jobSpec,
                SelectedPlayer = this.rankedPlayer,
                SelectedCode = this.rankedCode,
                FightId = this.fightId,
                ActorId = this.actorId,
                PreferredFightId = this.preferredFightId,
                IncludeOffGcds = this.includeOffGcds,
                Playback = this.playback,
                Index = this.playback?.Index ?? 0,
                Mismatch = this.mismatch,
                GuideTitle = this.guideTitle,
                DutyStatus = this.dutyStatus,
            };
        }
    }

    public void ForgetToken() => this.client.ForgetToken();

    public void SelectUltimate(int selectedEncounterId)
    {
        var reload = false;
        lock (this.gate)
        {
            if (this.encounterId == selectedEncounterId)
                return;

            this.encounterId = selectedEncounterId;
            this.ClearLoadedLocked();
            reload = this.jobSpec is not null;
        }

        this.configuration.LastEncounterId = selectedEncounterId;
        this.configuration.Save();
        if (reload)
            this.SearchRankings();
    }

    public void SelectJob(string specOrName)
    {
        var job = JobCatalog.Find(specOrName);
        if (job is null)
            return;

        lock (this.gate)
        {
            if (this.jobSpec == job.SpecName && this.rankings.Count > 0)
                return;

            var changed = this.jobSpec != job.SpecName;
            this.jobSpec = job.SpecName;
            if (changed)
                this.ClearLoadedLocked();
        }

        this.configuration.LastJobSpec = job.SpecName;
        this.configuration.Save();
        this.SearchRankings();
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
        int encounter;
        string? spec;
        lock (this.gate)
        {
            encounter = this.encounterId;
            spec = this.jobSpec;
        }

        if (string.IsNullOrWhiteSpace(spec))
        {
            lock (this.gate)
            {
                this.error = "Choose a job.";
                this.status = string.Empty;
                this.busy = false;
            }

            return;
        }

        var call = this.Begin();
        _ = this.SearchAsync(encounter, spec, call);
    }

    public void OpenRank(RankedReport row)
    {
        string? spec;
        lock (this.gate)
        {
            this.preferredFightId = row.FightId;
            this.rankedPlayer = row.Player;
            this.rankedCode = row.Code;
            spec = this.jobSpec;
        }

        var call = this.Begin();
        _ = this.OpenRankAsync(row, spec, call);
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
            var job = JobCatalog.Find(this.jobSpec)?.EnglishName;
            var title = string.IsNullOrWhiteSpace(job)
                ? $"{UltimateCatalog.GetOrFirst(this.encounterId).Alias} · {input.PlayerName}"
                : $"{UltimateCatalog.GetOrFirst(this.encounterId).Alias} · {job} · {input.PlayerName}";
            this.playback = new GuidePlayback(steps);
            this.mismatch = false;
            this.guideTitle = title;
            this.busy = false;
            this.error = string.Empty;
            this.status = steps.Count == 0
                ? "The log had no abilities after filtering."
                : $"{steps.Count} steps. In the selected ultimate, the guide follows the pull.";
            this.AlignToActivePull();
        }

        return steps.Count > 0 && announce;
    }

    /// <summary>
    /// Follows the selected ultimate the way a duty check does: territory id from
    /// ClientState.TerritoryType, and pull start from the rising edge of InCombat.
    /// Leaving that territory or leaving combat resets the guide. This does not press anything.
    /// </summary>
    public DutySync SyncDuty(ushort territoryType, bool inCombat, DateTime utcNow)
    {
        lock (this.gate)
        {
            var selected = UltimateCatalog.GetOrFirst(this.encounterId);
            var inSelectedZone = territoryType != 0 && territoryType == selected.TerritoryId;

            if (!inSelectedZone)
            {
                if (!this.inDutyZone && !this.pullActive)
                    return DutySync.None;

                var tell = this.playback is not null && (this.pullActive || this.playback.Index != 0);
                this.inDutyZone = false;
                this.pullActive = false;
                this.pullStartedUtc = null;
                this.playback?.Reset();
                this.mismatch = false;
                this.dutyStatus = tell ? "Left the duty. Guide reset." : string.Empty;
                return tell ? DutySync.Reset : DutySync.None;
            }

            if (!this.inDutyZone)
            {
                this.inDutyZone = true;
                this.dutyStatus = inCombat
                    ? $"In {selected.EnglishName}."
                    : $"In {selected.EnglishName}. Waiting for the pull.";
            }

            if (inCombat && !this.pullActive)
            {
                this.pullActive = true;
                this.pullStartedUtc = utcNow;
                this.mismatch = false;
                this.AlignToActivePull(utcNow);
                this.dutyStatus = this.playback is null
                    ? $"In {selected.EnglishName}. Load a guide to follow the pull."
                    : "Following the pull.";
                return this.playback is null ? DutySync.None : DutySync.PullStarted;
            }

            if (!inCombat && this.pullActive)
            {
                this.pullActive = false;
                this.pullStartedUtc = null;
                this.mismatch = false;
                this.playback?.Reset();
                this.dutyStatus = "Wipe. Guide reset.";
                return this.playback is null ? DutySync.None : DutySync.Reset;
            }

            if (this.pullActive)
                this.AlignToActivePull(utcNow);

            return DutySync.None;
        }
    }

    public void NotifyLocalAction(uint actionId, uint adjustedActionId)
    {
        if (AbilityNoise.IsAlwaysDropped(actionId) || AbilityNoise.IsAlwaysDropped(adjustedActionId))
            return;

        lock (this.gate)
        {
            if (this.playback is null || this.playback.IsComplete)
                return;

            if (this.pullActive)
            {
                var current = this.playback.Current;
                this.mismatch = current is not null && !current.Matches(actionId, adjustedActionId);
                return;
            }

            this.mismatch = !this.playback.TryAdvance(actionId, adjustedActionId);
        }
    }

    public void Skip()
    {
        lock (this.gate)
        {
            if (this.pullActive)
                return;

            this.playback?.Skip();
            this.mismatch = false;
        }
    }

    public void Back()
    {
        lock (this.gate)
        {
            if (this.pullActive)
                return;

            this.playback?.Back();
            this.mismatch = false;
        }
    }

    public void Reset()
    {
        lock (this.gate)
        {
            if (this.pullActive)
                return;

            this.playback?.Reset();
            this.mismatch = false;
        }
    }

    private void AlignToActivePull(DateTime? utcNow = null)
    {
        if (!this.pullActive || this.playback is null || this.pullStartedUtc is not { } started)
            return;

        var now = utcNow ?? DateTime.UtcNow;
        this.playback.AlignToPull((now - started).TotalMilliseconds);
        this.dutyStatus = "Following the pull.";
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

    private async Task SearchAsync(int encounter, string spec, (int Generation, CancellationToken Token) call)
    {
        try
        {
            var rows = await this.client.GetJobSpeedRankingsAsync(
                this.configuration.ClientId,
                this.configuration.ClientSecret,
                encounter,
                spec,
                call.Token).ConfigureAwait(false);

            lock (this.gate)
            {
                if (call.Generation != this.generation)
                    return;

                this.rankings = rows;
                this.busy = false;
                var job = JobCatalog.Find(spec)?.EnglishName ?? spec;
                this.status = rows.Count == 0
                    ? $"No speed rankings for {job}."
                    : $"{rows.Count} {job} speed rankings.";
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

    private async Task OpenRankAsync(RankedReport row, string? spec, (int Generation, CancellationToken Token) call)
    {
        try
        {
            var encounter = this.encounterId;
            var loaded = await this.client.GetReportAsync(
                this.configuration.ClientId,
                this.configuration.ClientSecret,
                row.Code,
                encounter,
                call.Token).ConfigureAwait(false);

            var fight = loaded.Fights.FirstOrDefault(item => item.Id == row.FightId);
            if (fight is null)
                throw new FflogsException("That pull is not in this report.");

            var actor = FindRankedPlayer(loaded, fight, row.Player, spec);
            if (actor is null)
                throw new FflogsException($"Could not find {row.Player} in this pull.");

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

                this.report = loaded;
                this.fightId = fight.Id;
                this.actorId = actor.Id;
                this.lastInput = input;
                this.pending = input;
                this.announceGuide = true;
                this.playback = null;
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

    private static ReportActorInfo? FindRankedPlayer(LoadedReport loaded, ReportFightInfo fight, string playerName, string? spec)
    {
        var players = loaded.PlayersIn(fight)
            .Where(player => string.Equals(player.Name, playerName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var job = JobCatalog.Find(spec);
        if (job is not null)
        {
            var byJob = players.Where(player => JobCatalog.Matches(job, player.Job)).ToList();
            if (byJob.Count > 0)
                players = byJob;
        }

        return players.FirstOrDefault();
    }

    private void ClearLoadedLocked()
    {
        this.rankings = [];
        this.report = null;
        this.fightId = null;
        this.actorId = null;
        this.preferredFightId = null;
        this.rankedPlayer = null;
        this.rankedCode = null;
        this.playback = null;
        this.pending = null;
        this.lastInput = null;
        this.guideTitle = string.Empty;
        this.error = string.Empty;
        this.status = string.Empty;
        this.announceGuide = false;
        this.mismatch = false;
        this.busy = false;
        this.requests.Cancel();
        this.requests = new CancellationTokenSource();
        this.generation++;
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
