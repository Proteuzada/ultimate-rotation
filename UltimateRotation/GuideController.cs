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

    public bool AutoRotation { get; init; }

    public bool StartOnCountdown { get; init; }

    public bool QueueButtonPresses { get; init; } = true;

    public bool UseGemdraught { get; init; }

    public bool ShowSkills { get; init; }

    public bool SkillsDetached { get; init; }

    public bool OpenerTraining { get; init; }

    public IReadOnlyList<ActionChoice> Actions { get; init; } = [];
}

internal enum DutySync
{
    None,
    PullStarted,
    CountdownStarted,
    DummyStarted,
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
    private bool combatSeen;
    private bool countdownPull;
    private DateTime? pullStartedUtc;
    private string dutyStatus = string.Empty;
    private bool autoRotation;
    private bool startOnCountdown;
    private bool queueButtonPresses;
    private bool stepQueued;
    private uint queuedActionId;
    private bool useGemdraught;
    private bool showSkills;
    private bool skillsDetached;
    private readonly CountdownGate countdown = new();
    private readonly PartPace pace = new();
    private bool dummyPractice;
    private bool openerTraining;
    private GuidePlayback? parsePlayback;
    private string parseTitle = string.Empty;
    private string parseStatus = string.Empty;
    private readonly HashSet<uint> disabledActions;
    private readonly HashSet<uint> enabledMitigations;
    private List<LogAction> logActions = [];
    private bool sawGemdraught;

    public GuideController(Configuration configuration)
    {
        this.configuration = configuration;
        this.encounterId = UltimateCatalog.GetOrFirst(configuration.LastEncounterId).EncounterId;
        this.jobSpec = JobCatalog.Find(configuration.LastJobSpec)?.SpecName;
        this.includeOffGcds = configuration.IncludeOffGcds;
        if (configuration.Version < 2)
        {
            configuration.QueueButtonPresses = true;
            configuration.Version = 2;
            configuration.Save();
        }

        this.autoRotation = configuration.AutoRotation;
        this.startOnCountdown = configuration.StartOnCountdown;
        this.queueButtonPresses = configuration.QueueButtonPresses;
        this.useGemdraught = configuration.UseGemdraught;
        this.showSkills = configuration.ShowSkills;
        this.skillsDetached = configuration.SkillsDetached;
        this.openerTraining = configuration.OpenerTraining;
        this.disabledActions = configuration.DisabledActionIds.ToHashSet();
        this.enabledMitigations = configuration.EnabledMitigationIds.ToHashSet();
        if (this.openerTraining)
        {
            lock (this.gate)
                this.ShowOpenerLocked();
        }
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
                AutoRotation = this.autoRotation,
                StartOnCountdown = this.startOnCountdown,
                QueueButtonPresses = this.queueButtonPresses,
                UseGemdraught = this.useGemdraught,
                ShowSkills = this.showSkills,
                SkillsDetached = this.skillsDetached,
                OpenerTraining = this.openerTraining,
                Actions = ActionMenu.Build(
                    this.jobSpec,
                    this.logActions,
                    this.sawGemdraught,
                    this.useGemdraught,
                    this.disabledActions,
                    this.enabledMitigations),
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
            if (this.openerTraining)
                this.ShowOpenerLocked();
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
            {
                this.ClearLoadedLocked();
                if (this.openerTraining)
                    this.ShowOpenerLocked();
            }
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
            if (this.openerTraining)
                this.ShowOpenerLocked();
        }

        this.configuration.IncludeOffGcds = include;
        this.configuration.Save();
    }

    public void SetAutoRotation(bool enabled)
    {
        lock (this.gate)
        {
            this.autoRotation = enabled;
            if (this.pullActive && this.playback is not null)
                this.dutyStatus = enabled
                    ? "Following the pull. Automatic rotation is on."
                    : "Following the pull.";
        }

        this.configuration.AutoRotation = enabled;
        this.configuration.Save();
    }

    public void SetStartOnCountdown(bool enabled)
    {
        lock (this.gate)
        {
            this.startOnCountdown = enabled;
            if (!enabled)
                this.countdown.Reset();
        }

        this.configuration.StartOnCountdown = enabled;
        this.configuration.Save();
    }

    public void SetQueueButtonPresses(bool enabled)
    {
        lock (this.gate)
        {
            this.queueButtonPresses = enabled;
            if (!enabled)
            {
                this.stepQueued = false;
                this.queuedActionId = 0;
            }
        }

        this.configuration.QueueButtonPresses = enabled;
        this.configuration.Save();
    }

    public void SetShowSkills(bool enabled)
    {
        lock (this.gate)
            this.showSkills = enabled;

        this.configuration.ShowSkills = enabled;
        this.configuration.Save();
    }

    public void SetSkillsDetached(bool detached)
    {
        lock (this.gate)
        {
            if (this.skillsDetached == detached)
                return;

            this.skillsDetached = detached;
        }

        this.configuration.SkillsDetached = detached;
        this.configuration.Save();
    }

    public void SetOpenerTraining(bool enabled)
    {
        lock (this.gate)
        {
            if (this.openerTraining == enabled)
                return;

            this.openerTraining = enabled;
            this.mismatch = false;
            this.stepQueued = false;
            this.queuedActionId = 0;
            if (enabled)
            {
                this.ClearPull();
                this.ShowOpenerLocked();
            }
            else
            {
                this.ShowParseLocked();
                if (this.pullActive)
                    this.AlignToActivePull();
            }
        }

        this.configuration.OpenerTraining = enabled;
        this.configuration.Save();
    }

    public void SetUseGemdraught(bool enabled)
    {
        lock (this.gate)
        {
            this.useGemdraught = enabled;
            if (this.lastInput is not null)
                this.pending = this.lastInput;
            if (this.openerTraining)
                this.ShowOpenerLocked();
        }

        this.configuration.UseGemdraught = enabled;
        this.configuration.Save();
    }

    public void SetActionEnabled(uint actionId, bool mitigation, bool potion, bool enabled)
    {
        if (potion)
        {
            this.SetUseGemdraught(enabled);
            return;
        }

        lock (this.gate)
        {
            if (mitigation)
            {
                if (enabled)
                    this.enabledMitigations.Add(actionId);
                else
                    this.enabledMitigations.Remove(actionId);
                this.disabledActions.Remove(actionId);
            }
            else if (enabled)
            {
                this.disabledActions.Remove(actionId);
            }
            else
            {
                this.disabledActions.Add(actionId);
            }

            if (this.lastInput is not null)
                this.pending = this.lastInput;
        }

        this.configuration.DisabledActionIds = this.disabledActions.ToList();
        this.configuration.EnabledMitigationIds = this.enabledMitigations.ToList();
        this.configuration.Save();
    }

    public void SetDamageActionsEnabled(bool enabled)
    {
        lock (this.gate)
        {
            foreach (var action in this.logActions)
            {
                if (MitigationCatalog.IsMitigation(action.ActionId, action.Name))
                    continue;
                if (enabled)
                    this.disabledActions.Remove(action.ActionId);
                else
                    this.disabledActions.Add(action.ActionId);
            }

            if (this.lastInput is not null)
                this.pending = this.lastInput;
        }

        this.configuration.DisabledActionIds = this.disabledActions.ToList();
        this.configuration.Save();
    }

    public void SetMitigationsEnabled(bool enabled)
    {
        lock (this.gate)
        {
            foreach (var mitigation in MitigationCatalog.ForJob(this.jobSpec))
            {
                if (enabled)
                    this.enabledMitigations.Add(mitigation.ActionId);
                else
                    this.enabledMitigations.Remove(mitigation.ActionId);
            }

            foreach (var action in this.logActions.Where(action => MitigationCatalog.IsMitigation(action.ActionId, action.Name)))
            {
                if (enabled)
                    this.enabledMitigations.Add(action.ActionId);
                else
                    this.enabledMitigations.Remove(action.ActionId);
            }

            if (this.lastInput is not null)
                this.pending = this.lastInput;
        }

        this.configuration.EnabledMitigationIds = this.enabledMitigations.ToList();
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
        var drink = false;
        HashSet<uint> disabled = [];
        HashSet<uint> enabledMitigations = [];
        lock (this.gate)
        {
            input = this.pending;
            this.pending = null;
            include = this.includeOffGcds;
            announce = this.announceGuide;
            this.announceGuide = false;
            drink = this.useGemdraught;
            disabled = this.disabledActions.ToHashSet();
            enabledMitigations = this.enabledMitigations.ToHashSet();
        }

        if (input is null)
            return false;

        IReadOnlyList<GuideStep> steps;
        try
        {
            steps = TimelineNormalizer.Normalize(
                input,
                actionId => GameActions.Classify(actionId, Plugin.DataManager, include));
            var named = steps
                .Select(step => step with
                {
                    Name = GameActions.ClientName(step.ActionId, Plugin.DataManager) ?? step.Name,
                    AdjustedActionId = GameActions.Adjusted(step.ActionId),
                })
                .ToList();
            lock (this.gate)
            {
                this.sawGemdraught = named.Any(step => Gemdraughts.IsStep(step.Name));
                this.logActions = named
                    .Where(step => !Gemdraughts.IsStep(step.Name) && step.ActionId != 0)
                    .GroupBy(step => step.ActionId)
                    .Select(group => new LogAction(group.Key, group.First().Name, group.First().Kind))
                    .ToList();
            }

            steps = named
                .Where(step => ActionMenu.Allows(step.ActionId, step.Name, drink, disabled, enabledMitigations))
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
            var built = new GuidePlayback(steps);
            var builtStatus = steps.Count == 0
                ? "The log had no abilities after filtering."
                : $"{steps.Count} steps. In the selected ultimate, the guide follows the pull.";
            this.parsePlayback = built;
            this.parseTitle = title;
            this.parseStatus = builtStatus;
            this.busy = false;
            this.error = string.Empty;
            this.status = builtStatus;
            if (this.openerTraining)
                return steps.Count > 0 && announce;

            this.playback = built;
            this.pace.Reset();
            this.stepQueued = false;
            this.queuedActionId = 0;
            this.mismatch = false;
            this.guideTitle = title;
            this.AlignToActivePull();
            if (this.autoRotation && this.pullActive)
                this.dutyStatus = "Following the pull. Automatic rotation is on.";
        }

        return steps.Count > 0 && announce;
    }

    /// <summary>
    /// Follows the selected ultimate the way a duty check does: territory id from
    /// ClientState.TerritoryType, and pull start from the rising edge of InCombat.
    /// Leaving that territory or leaving combat resets the guide. Pressing a step is separate
    /// and only happens when automatic rotation is on.
    /// </summary>
    public DutySync SyncDuty(uint territoryType, bool inCombat, bool strikingDummy, DateTime utcNow)
    {
        lock (this.gate)
        {
            if (this.openerTraining)
                return this.SyncOpenerPractice(territoryType, inCombat, strikingDummy, utcNow);

            var selected = UltimateCatalog.GetOrFirst(this.encounterId);
            var inSelectedZone = territoryType != 0 && territoryType == selected.TerritoryId;

            if (!inSelectedZone)
            {
                if ((this.autoRotation || this.startOnCountdown) && strikingDummy)
                    return this.SyncDummy(utcNow, inCombat);

                if (!this.inDutyZone && !this.pullActive && !this.dummyPractice)
                    return DutySync.None;

                var tell = this.playback is not null && (this.pullActive || this.playback.Index != 0);
                var onDummy = this.dummyPractice;
                this.inDutyZone = false;
                this.dummyPractice = false;
                this.ClearPull();
                this.playback?.Reset();
                this.mismatch = false;
                this.dutyStatus = tell
                    ? onDummy ? "Left the striking dummy. Guide reset." : "Left the duty. Guide reset."
                    : string.Empty;
                return tell ? DutySync.Reset : DutySync.None;
            }

            if (this.dummyPractice)
            {
                this.dummyPractice = false;
                this.ClearPull();
                this.playback?.Reset();
                this.mismatch = false;
            }

            if (!this.inDutyZone)
            {
                this.inDutyZone = true;
                this.dutyStatus = inCombat
                    ? $"In {selected.EnglishName}."
                    : $"In {selected.EnglishName}. Waiting for the pull.";
            }

            if (this.CountdownExpired(utcNow))
                return this.playback is null ? DutySync.None : DutySync.Reset;

            if (inCombat && !this.pullActive)
            {
                this.pullActive = true;
                this.combatSeen = true;
                this.countdownPull = false;
                this.pullStartedUtc = utcNow;
                this.mismatch = false;
                this.AlignToActivePull(utcNow);
                this.dutyStatus = this.playback is null
                    ? $"In {selected.EnglishName}. Load a guide to follow the pull."
                    : this.autoRotation
                        ? "Following the pull. Automatic rotation is on."
                        : "Following the pull.";
                return this.playback is null ? DutySync.None : DutySync.PullStarted;
            }

            if (inCombat && this.pullActive)
                this.combatSeen = true;

            if (!inCombat && this.pullActive && this.combatSeen)
            {
                this.ClearPull();
                this.mismatch = false;
                this.playback?.Reset();
                this.dutyStatus = "Wipe. Guide reset.";
                return this.playback is null ? DutySync.None : DutySync.Reset;
            }

            if (this.pullActive && !this.autoRotation && !this.countdownPull && this.playback is { WaitingForFirstSkill: true })
                this.AlignToActivePull(utcNow);

            return DutySync.None;
        }
    }

    /// <summary>
    /// Practices the opener in the selected ultimate or on a striking dummy.
    /// The clock can start, but the index is not snapped to the fight time and a wipe does not reset it.
    /// </summary>
    private DutySync SyncOpenerPractice(uint territoryType, bool inCombat, bool strikingDummy, DateTime utcNow)
    {
        var selected = UltimateCatalog.GetOrFirst(this.encounterId);
        var inSelectedZone = territoryType != 0 && territoryType == selected.TerritoryId;
        var canPractice = inSelectedZone || strikingDummy;
        if (!canPractice)
        {
            var wasActive = this.pullActive || this.inDutyZone || this.dummyPractice;
            this.inDutyZone = false;
            this.dummyPractice = false;
            if (this.pullActive)
                this.ClearPull();

            this.mismatch = false;
            if (wasActive)
                this.dutyStatus = "Opener practice paused. The queue stays where you left it.";

            return DutySync.None;
        }

        if (inSelectedZone)
        {
            if (this.dummyPractice)
            {
                this.dummyPractice = false;
                if (this.pullActive)
                    this.ClearPull();
            }

            if (!this.inDutyZone)
            {
                this.inDutyZone = true;
                this.dutyStatus = inCombat
                    ? $"In {selected.EnglishName}. Practicing the opener."
                    : $"In {selected.EnglishName}. Waiting to practice the opener.";
            }
        }
        else if (!this.dummyPractice)
        {
            this.dummyPractice = true;
            this.inDutyZone = false;
            this.dutyStatus = "Striking dummy. Practicing the opener.";
        }

        if (this.pullActive && !this.combatSeen && this.pullStartedUtc is { } started
            && (utcNow - started).TotalSeconds > 8)
        {
            this.ClearPull();
            this.mismatch = false;
            this.dutyStatus = "The countdown ended without a pull. The opener stays where you left it.";
            return DutySync.None;
        }

        if (inCombat && !this.pullActive)
        {
            this.pullActive = true;
            this.combatSeen = true;
            this.countdownPull = false;
            this.pullStartedUtc = utcNow;
            this.mismatch = false;
            this.dutyStatus = this.autoRotation
                ? "Practicing the opener. Automatic rotation is on."
                : "Practicing the opener. After the first skill, a later press marks skipped steps with an X.";
            if (this.playback is null)
                return DutySync.None;

            if (!inSelectedZone && !this.autoRotation)
                return DutySync.None;

            return inSelectedZone ? DutySync.PullStarted : DutySync.DummyStarted;
        }

        if (inCombat && this.pullActive)
            this.combatSeen = true;

        if (!inCombat && this.pullActive && this.combatSeen)
        {
            this.ClearPull();
            this.mismatch = false;
            this.dutyStatus = "Opener practice paused. The queue stays where you left it.";
            return DutySync.None;
        }

        return DutySync.None;
    }

    private DutySync SyncDummy(DateTime utcNow, bool inCombat)
    {
        if (!this.dummyPractice)
        {
            this.dummyPractice = true;
            this.inDutyZone = false;
            this.dutyStatus = this.playback is null
                ? "Striking dummy. Load a guide to practice."
                : "Striking dummy. Waiting for combat.";
        }

        if (this.CountdownExpired(utcNow))
            return this.playback is null ? DutySync.None : DutySync.Reset;

        if (inCombat && !this.pullActive)
        {
            this.pullActive = true;
            this.combatSeen = true;
            this.countdownPull = false;
            this.pullStartedUtc = utcNow;
            this.mismatch = false;
            this.AlignToActivePull(utcNow);
            this.dutyStatus = this.playback is null
                ? "Striking dummy. Load a guide to practice."
                : "Practicing on a striking dummy.";
            return this.playback is null ? DutySync.None : DutySync.DummyStarted;
        }

        if (inCombat && this.pullActive)
            this.combatSeen = true;

        if (!inCombat && this.pullActive && this.combatSeen)
        {
            this.ClearPull();
            this.mismatch = false;
            this.playback?.Reset();
            this.dutyStatus = "Dummy practice reset.";
            return this.playback is null ? DutySync.None : DutySync.Reset;
        }

        return DutySync.None;
    }

    /// <summary>
    /// When start-on-countdown is on, the guide clock begins at the end of the displayed 1,
    /// while that number is still showing. It does not wait until the number disappears.
    /// </summary>
    public DutySync SyncCountdown(bool showing, float remainingSeconds, DateTime utcNow)
    {
        lock (this.gate)
        {
            if (!this.startOnCountdown)
            {
                this.countdown.Reset();
                return DutySync.None;
            }

            var finished = this.countdown.Observe(showing, remainingSeconds);
            if (!finished || this.playback is null || (!this.inDutyZone && !this.dummyPractice))
                return DutySync.None;

            if (this.pullActive)
            {
                if (this.pullStartedUtc is { } started && (utcNow - started).TotalSeconds < 2)
                    this.countdownPull = true;
                return DutySync.None;
            }

            this.pullActive = true;
            this.combatSeen = false;
            this.countdownPull = true;
            this.pullStartedUtc = utcNow;
            this.mismatch = false;
            if (this.openerTraining)
            {
                this.dutyStatus = "Countdown is at the end of the 1. The opener starts now.";
                return DutySync.CountdownStarted;
            }

            this.AlignToActivePull(utcNow);
            this.dutyStatus = "Countdown is at the end of the 1. The current step is due now.";
            return DutySync.CountdownStarted;
        }
    }

    /// <summary>
    /// Plays the loaded parse as one queue per fight part. UseAction is sent only after the
    /// animation lock is clear: a press during the lock is rejected. A GCD is sent in the last
    /// half-second of its recast. An oGCD does not wait for that recast. A press the game does
    /// not keep is tried again. A skill the game can cast is held until it fires. A press already
    /// stored by the game is not replaced. Only an action the game can never cast is skipped,
    /// and not before the first skill of the pull is used. One new press per frame.
    /// </summary>
    public void ExecuteDueStep(DateTime utcNow)
    {
        var released = false;
        var waitingId = 0u;
        lock (this.gate)
        {
            if (!this.CanPress)
                return;

            if (this.stepQueued)
            {
                released = true;
                waitingId = this.queuedActionId;
            }
        }

        if (released)
        {
            var stillQueued = LocalPlayerAction.IsQueued(waitingId);
            lock (this.gate)
            {
                if (!this.stepQueued)
                    return;

                if (stillQueued)
                    return;

                // The slot closed without the cast. The step stays due and is pressed again.
                this.stepQueued = false;
                this.queuedActionId = 0;
            }
        }

        GuideStep? step = null;
        IReadOnlyList<GuideStep>? steps = null;
        var elapsed = 0d;
        var drink = false;
        string? spec = null;
        var queue = false;
        lock (this.gate)
        {
            if (!this.CanPress || this.stepQueued)
                return;

            elapsed = (utcNow - this.pullStartedUtc!.Value).TotalMilliseconds;
            this.EnterCurrentPart(elapsed);
            steps = this.playback!.Steps;
            step = this.playback.Current;
            if (step is null)
                return;

            drink = this.useGemdraught;
            spec = this.jobSpec;
            queue = this.queueButtonPresses;
        }

        if (step is null || steps is null)
            return;

        var actionId = step.AdjustedActionId != 0 ? step.AdjustedActionId : step.ActionId;
        var potion = Gemdraughts.IsStep(step.Name);
        var animation = potion ? 0 : LocalPlayerAction.AnimationLockMs();
        var gcdRemain = potion ? 0 : LocalPlayerAction.WeaponRemainMs();
        if (!potion && step.Kind == StepKind.Gcd)
            gcdRemain = Math.Max(gcdRemain, LocalPlayerAction.RecastRemainingMs(actionId));
        var decision = RotationQueue.Decide(
            steps,
            step.Index,
            elapsed,
            LocalPlayerAction.SlotOccupied(),
            queue && !potion,
            animation,
            gcdRemain);
        if (decision.PressIndex != step.Index)
            return;

        var (attempt, pressedId) = this.ChooseAttempt(potion, drink, spec, step, actionId, elapsed);

        lock (this.gate)
        {
            if (this.playback?.Current is not { } current || current.Index != step.Index)
                return;

            if (attempt == ActionAttempt.Queued)
            {
                this.stepQueued = true;
                this.queuedActionId = LocalPlayerAction.LastSentId != 0
                    ? LocalPlayerAction.LastSentId
                    : pressedId;
                this.mismatch = false;
                return;
            }

            if (attempt == ActionAttempt.Blocked)
                return;

            if (attempt == ActionAttempt.Impossible && this.playback.WaitingForFirstSkill && !potion)
                return;

            if (attempt == ActionAttempt.Impossible)
            {
                this.playback.Skip();
                this.mismatch = false;
                return;
            }

            // The cast already happened and the hook did not move the index.
            // Potions never reach the hook. A combat cast the hook saw returned above.
            this.pace.NoteLate(elapsed, current.OffsetMs);
            this.playback.Skip();
            this.mismatch = false;
        }
    }

    private bool IsOwnCast(uint actionId, uint adjusted)
    {
        if (LocalPlayerAction.IsExpected(actionId, adjusted))
            return true;

        if (!this.stepQueued || this.queuedActionId == 0)
            return false;

        return actionId == this.queuedActionId || adjusted == this.queuedActionId;
    }

    public void NotifyLocalAction(uint actionId, uint adjustedActionId)
    {
        if (AbilityNoise.IsAlwaysDropped(actionId) || AbilityNoise.IsAlwaysDropped(adjustedActionId))
            return;

        var usedName = GameActions.ClientName(
            adjustedActionId != 0 ? adjustedActionId : actionId,
            Plugin.DataManager);

        lock (this.gate)
        {
            if (this.playback is null || this.playback.IsComplete)
                return;

            if (!this.pullActive && !this.openerTraining)
            {
                this.mismatch = !this.playback.TryAdvance(actionId, adjustedActionId);
                return;
            }

            if (this.IsOwnCast(actionId, adjustedActionId))
            {
                this.stepQueued = false;
                this.queuedActionId = 0;
                this.playback.Skip();
                this.mismatch = false;
                return;
            }

            if (this.playback.JumpToUsed(actionId, adjustedActionId, usedName))
            {
                this.stepQueued = false;
                this.queuedActionId = 0;
                this.mismatch = false;
                return;
            }

            if (!this.playback.FirstSkillUsed)
            {
                this.mismatch = false;
                return;
            }

            var current = this.playback.Current;
            this.mismatch = current is not null && !current.Matches(actionId, adjustedActionId)
                && (string.IsNullOrWhiteSpace(usedName)
                    || !string.Equals(current.Name, usedName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private (ActionAttempt Attempt, uint PressedId) ChooseAttempt(
        bool potion,
        bool drink,
        string? spec,
        GuideStep step,
        uint actionId,
        double elapsed)
    {
        if (potion)
        {
            var drinkAttempt = drink
                ? GemdraughtUse.Use(Gemdraughts.StatFor(step.Name, spec))
                : ActionAttempt.Impossible;
            return (drinkAttempt, actionId);
        }

        var logged = LocalPlayerAction.Availability(actionId);
        uint? partner = null;
        StepAvailability? partnerState = null;
        if (OffGcdPairs.MaySwap(step.Kind, elapsed, step.OffsetMs)
            && OffGcdPairs.Partner(step.ActionId, step.AdjustedActionId, step.Name) is uint other)
        {
            partner = other;
            partnerState = LocalPlayerAction.Availability(other);
        }

        if (StuckStep.ShouldSkip(logged, partner is null ? null : partnerState))
            return (ActionAttempt.Impossible, actionId);

        if (logged != StepAvailability.Press && partner is uint ready && partnerState == StepAvailability.Press)
            return (LocalPlayerAction.Consider(ready), ready);

        var attempt = LocalPlayerAction.Consider(actionId);
        if (attempt == ActionAttempt.Blocked && partner is uint fallback && partnerState != StepAvailability.Skip)
        {
            var swapped = LocalPlayerAction.Consider(fallback);
            if (swapped is ActionAttempt.Used or ActionAttempt.Queued)
                return (swapped, fallback);
        }

        return (attempt, actionId);
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

    private bool CanPress
        => (this.autoRotation || this.countdownPull) && this.pullActive && this.playback is not null && this.pullStartedUtc is not null;

    private void ClearPull()
    {
        this.pullActive = false;
        this.combatSeen = false;
        this.countdownPull = false;
        this.pullStartedUtc = null;
        this.stepQueued = false;
        this.queuedActionId = 0;
        this.pace.Reset();
    }

    /// <summary>
    /// Moves to the first step of a part once the fight clock reaches that part.
    /// A part that is still early stays ahead of the index. The first visit to a
    /// part anchors its timing so a previous delay does not dump every skill in it.
    /// </summary>
    private void EnterCurrentPart(double elapsedMs)
    {
        if (this.playback is null)
            return;

        if (!this.playback.WaitingForFirstSkill)
        {
            var enter = this.playback.PartIndexAt(elapsedMs);
            while (this.playback.Index < enter && !this.playback.IsComplete)
                this.playback.Skip();
        }

        if (this.playback.Current is not { } step)
            return;

        this.pace.ObserveStep(elapsedMs, step.Index, this.playback.CurrentStartsPart, step.OffsetMs);
    }

    private bool CountdownExpired(DateTime utcNow)
    {
        if (!this.pullActive || this.combatSeen || this.pullStartedUtc is not { } started)
            return false;

        if ((utcNow - started).TotalSeconds <= 8)
            return false;

        this.ClearPull();
        this.mismatch = false;
        this.playback?.Reset();
        this.dutyStatus = "The countdown ended without a pull. Guide reset.";
        return true;
    }

    private void AlignToActivePull(DateTime? utcNow = null)
    {
        if (!this.pullActive || this.playback is null || this.pullStartedUtc is not { } started)
            return;

        var now = utcNow ?? DateTime.UtcNow;
        this.playback.AlignToPull((now - started).TotalMilliseconds);
        this.dutyStatus = "Following the pull.";
    }

    private void ShowOpenerLocked()
    {
        var job = JobCatalog.Find(this.jobSpec);
        var steps = BalanceOpeners.For(this.jobSpec, this.includeOffGcds, this.useGemdraught);
        this.stepQueued = false;
        this.queuedActionId = 0;
        this.mismatch = false;
        this.pace.Reset();
        if (job is null || steps is null || steps.Count == 0)
        {
            this.playback = null;
            this.guideTitle = string.Empty;
            this.dutyStatus = "Pick a job on the Parses page to practice its opener.";
            return;
        }

        this.playback = new GuidePlayback(Present(steps));
        this.guideTitle = $"The Balance opener · {job.EnglishName}";
        this.dutyStatus = "Practicing the opener. After the first skill, a later press marks skipped steps with an X and jumps the queue.";
        if (this.pullActive)
            this.pullStartedUtc = DateTime.UtcNow;
    }

    private void ShowParseLocked()
    {
        this.playback = this.parsePlayback;
        this.guideTitle = this.parseTitle;
        this.pace.Reset();
        this.stepQueued = false;
        this.queuedActionId = 0;
        this.mismatch = false;
        if (!string.IsNullOrEmpty(this.parseStatus))
            this.status = this.parseStatus;

        this.dutyStatus = this.playback is null
            ? string.Empty
            : "Showing the loaded parse.";
    }

    private static List<GuideStep> Present(IReadOnlyList<GuideStep> steps)
    {
        var presented = new List<GuideStep>(steps.Count);
        foreach (var step in steps)
        {
            if (step.ActionId == 0)
            {
                presented.Add(step);
                continue;
            }

            presented.Add(step with
            {
                Name = GameActions.ClientName(step.ActionId, Plugin.DataManager) ?? step.Name,
                AdjustedActionId = GameActions.Adjusted(step.ActionId),
            });
        }

        return presented;
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
            var rows = await this.client.GetJobParseRankingsAsync(
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
                    ? $"No {job} parses on recent kills."
                    : $"{rows.Count} {job} parses from the class rankings, grouped by percentile.";
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
                this.parsePlayback = null;
                this.parseTitle = string.Empty;
                this.parseStatus = string.Empty;
                if (!this.openerTraining)
                {
                    this.playback = null;
                    this.guideTitle = string.Empty;
                }

                this.pace.Reset();
                this.pending = null;
                this.lastInput = null;
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
                if (!this.openerTraining)
                    this.playback = null;
                this.pace.Reset();
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
        this.parsePlayback = null;
        this.parseTitle = string.Empty;
        this.parseStatus = string.Empty;
        this.pace.Reset();
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
