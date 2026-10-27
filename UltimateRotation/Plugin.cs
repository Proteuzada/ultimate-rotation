using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using UltimateRotation.Windows;

namespace UltimateRotation;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/guide";
    private const string CommandAlias = "/guia";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;

    private readonly WindowSystem windowSystem;
    private readonly GuideController controller;
    private readonly ActionObserver observer;
    private readonly SetupWindow setup;

    public Plugin()
    {
        var configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.controller = new GuideController(configuration);
        this.setup = new SetupWindow(configuration, this.controller);
        this.windowSystem = new WindowSystem("UltimateRotation");
        this.windowSystem.AddWindow(this.setup);

        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = UiText.CommandHelp,
        });
        CommandManager.AddHandler(CommandAlias, new CommandInfo(this.OnCommand)
        {
            HelpMessage = UiText.AliasHelp,
        });

        PluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += this.ToggleSetup;
        PluginInterface.UiBuilder.OpenMainUi += this.ToggleSetup;
        Framework.Update += this.OnFramework;
        this.observer = new ActionObserver(this.controller, GameInterop, Log);
    }

    public void Dispose()
    {
        Framework.Update -= this.OnFramework;
        PluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleSetup;
        PluginInterface.UiBuilder.OpenMainUi -= this.ToggleSetup;
        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandAlias);
        this.windowSystem.RemoveAllWindows();
        this.observer.Dispose();
        this.controller.Dispose();
    }

    private void OnFramework(IFramework framework)
    {
        var now = DateTime.UtcNow;
        var loaded = this.controller.Pump();
        var sync = this.controller.SyncDuty(
            ClientState.TerritoryType,
            Condition[ConditionFlag.InCombat],
            StrikingDummy.IsCurrentTarget(),
            now);
        var showingCountdown = PullCountdown.TryRead(out var remaining);
        var countdown = this.controller.SyncCountdown(showingCountdown, remaining, now);
        this.controller.ExecuteDueStep(now);

        if (loaded || sync is DutySync.PullStarted or DutySync.DummyStarted || countdown == DutySync.CountdownStarted)
            this.setup.ShowPage(SettingsPage.Rotation);

        if (countdown == DutySync.CountdownStarted)
            Chat.Print("Countdown is at 1. The current step is pressed now, and the rotation follows from here.");
        else if (sync == DutySync.PullStarted)
        {
            Chat.Print(this.controller.Snapshot().AutoRotation
                ? "Pull started. Automatic rotation will press each step when it is due."
                : "Pull started. The guide is following this fight. Automatic rotation is off.");
        }
        else if (sync == DutySync.DummyStarted)
            Chat.Print("Dummy practice started. Automatic rotation will press each step when it is due.");
        else if (loaded)
            Chat.Print("Guide loaded.");
        else if (sync == DutySync.Reset)
            Chat.Print("Guide reset.");
    }

    private void OnCommand(string command, string args)
    {
        var argument = args.Trim();
        if (argument.Equals("overlay", StringComparison.OrdinalIgnoreCase)
            || argument.Equals("rotation", StringComparison.OrdinalIgnoreCase))
            this.setup.ShowPage(SettingsPage.Rotation);
        else if (argument.Equals("actions", StringComparison.OrdinalIgnoreCase))
            this.setup.ShowPage(SettingsPage.Actions);
        else
            this.setup.Toggle();
    }

    private void ToggleSetup() => this.setup.Toggle();
}
