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

    private readonly WindowSystem windowSystem;
    private readonly GuideController controller;
    private readonly ActionObserver observer;
    private readonly SetupWindow setup;
    private readonly OverlayWindow overlay;

    public Plugin()
    {
        var configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.controller = new GuideController(configuration);
        this.setup = new SetupWindow(configuration, this.controller);
        this.overlay = new OverlayWindow(this.controller);
        this.windowSystem = new WindowSystem("UltimateRotation");
        this.windowSystem.AddWindow(this.setup);
        this.windowSystem.AddWindow(this.overlay);

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
        var loaded = this.controller.Pump();
        var sync = this.controller.SyncDuty(
            ClientState.TerritoryType,
            Condition[ConditionFlag.InCombat],
            DateTime.UtcNow);

        if (loaded || sync == DutySync.PullStarted)
            this.overlay.IsOpen = true;

        if (sync == DutySync.PullStarted)
            Chat.Print("Pull started. The guide is following this fight and does not press any button.");
        else if (loaded)
            Chat.Print("Guide loaded. It advances when you use the ability and does not press any button.");
        else if (sync == DutySync.Reset)
            Chat.Print("Guide reset.");
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("overlay", StringComparison.OrdinalIgnoreCase))
            this.overlay.Toggle();
        else
            this.setup.Toggle();
    }

    private void ToggleSetup() => this.setup.Toggle();
}
