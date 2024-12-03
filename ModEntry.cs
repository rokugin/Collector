using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;

namespace Collector;

internal class ModEntry : Mod {

    public static IMonitor SMonitor = null!;
    public static ModConfig Config = null!;
    public static ModConfigKeys Keys => Config.Controls;

    Collector Collector = new();
    Chest Chest = new();

    public override void Entry(IModHelper helper) {
        Config = helper.ReadConfig<ModConfig>();
        SMonitor = Monitor;
        
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        //helper.Events.GameLoop.DayStarted += Collector.OnDayStarted;
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e) {
        if (Collector.GetCollectorMutex().IsLocked() && Collector.GetCollectorMutex().IsLockHeld() && Game1.activeClickableMenu == null) {
            Collector.GetCollectorMutex().ReleaseLock();
        }
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e) {
        if (Context.IsPlayerFree) {
            if (Keys.ActivateLocalCollector.JustPressed()) {
                //Collector.DoCollection(Game1.player.currentLocation);
                Utility.ForEachLocation(location => {
                    Collector.DoCollection(location);
                    
                    return true;
                });
            }
            if (Keys.OpenCollectorInventory.JustPressed()) {
                if (Collector.GetCollectorMutex().IsLocked()) return;
                Collector.GetCollectorMutex().RequestLock(() => {
                    Collector.ShowCollectorInventory();
                });
            }
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e) {
        var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

        if (configMenu is null) return;

        configMenu.Register(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));

        configMenu.AddKeybindList(ModManifest, () => Config.Controls.ActivateLocalCollector, v => Config.Controls.ActivateLocalCollector = v,
            () => "Activate Collecting Keybinds");

        configMenu.AddKeybindList(ModManifest, () => Config.Controls.OpenCollectorInventory, v => Config.Controls.OpenCollectorInventory = v,
            () => "Open Collector Keybinds");
    }

}