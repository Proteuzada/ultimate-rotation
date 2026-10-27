using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using UltimateRotation.Windows;

namespace UltimateRotation;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/guia";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;

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
        this.windowSystem.RemoveAllWindows();
        this.observer.Dispose();
        this.controller.Dispose();
    }

    private void OnFramework(IFramework framework)
    {
        if (!this.controller.Pump())
            return;

        this.overlay.IsOpen = true;
        Chat.Print("Guia carregado. Ele avança com a sua habilidade e não aperta nenhum botão.");
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
