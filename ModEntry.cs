using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Numerics;
using SObject = StardewValley.Object;

namespace Collector;

internal class ModEntry : Mod {

    public static IMonitor SMonitor = null!;
    public static ModConfig Config = null!;
    public static ModConfigKeys Keys => Config.Controls;
    public static string CollectorID = "(BC)165";//"(BC)rokugin.collector_Collector";

    Collector Collector = new();
    List<GameLocation> collectorLocations = new();

    public override void Entry(IModHelper helper) {
        Config = helper.ReadConfig<ModConfig>();
        SMonitor = Monitor;
        
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        helper.Events.World.ObjectListChanged += OnObjectListChanged;
    }

    private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e) {
        foreach (var pair in e.Added) {
            if (pair.Value.QualifiedItemId == CollectorID) {
                if (!collectorLocations.Contains(e.Location)) {
                    collectorLocations.Add(e.Location);
                    DelayedAction action = new DelayedAction(500);
                    action.behavior = () => {
                        Collector.DoCollection(e.Location);
                    };
                    Log.Info($"\n{e.Location.NameOrUniqueName} added to Collector Locations.\n");
                    return;
                }
            }
        }

        foreach (var pair in e.Removed) {
            if (pair.Value.QualifiedItemId == CollectorID) {
                bool duplicate = false;
                foreach (var obj in e.Location.objects.Values) {
                    if (obj.QualifiedItemId == CollectorID) {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate) {
                    collectorLocations.Remove(e.Location);
                    Log.Info($"\n{e.Location.NameOrUniqueName} removed from Collector Locations.\n");
                    return;
                }
            }
        }
    }

    private void OnTimeChanged(object? sender, TimeChangedEventArgs e) {
        DelayedAction action = new DelayedAction(500);
        action.behavior = () => {
            List<Item> items = new();
            string output = $"\n=====Panning Spots=====\n";

            foreach (var location in collectorLocations) {
                items.AddRange(Collector.CollectPanningSpot(location));
            }

            List<Item> oldCollectorList = Collector.GetCollectorInventory().ToList();

            foreach (var item in items) {
                Collector.AddItem(item);
            }

            List<Item> newCollectorList = Collector.GetCollectorInventory().ToList();
            Collector.DoDebugInfo(oldCollectorList, newCollectorList);

            if (newCollectorList.Count > 0) {
                foreach (var item in newCollectorList) {
                    output += $"{item.Stack}x {(item.Quality != 0 ? Collector.GetQualityName(item.Quality) + " " : "")}{item.DisplayName} collected.\n";
                }

                Log.Info(output);
            }
        };
        Game1.delayedActions.Add(action);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e) {
        if (Collector.GetCollectorMutex().IsLocked() && Collector.GetCollectorMutex().IsLockHeld() && Game1.activeClickableMenu == null) {
            Collector.GetCollectorMutex().ReleaseLock();
        }
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e) {
        if (Context.IsPlayerFree) {
            if (Keys.ActivateLocalCollector.JustPressed()) {
                Collector.DoCollection(Game1.player.currentLocation);
                //Utility.ForEachLocation(location => {
                //    Collector.DoCollection(location);

                //    return true;
                //});
            }
            if (Keys.OpenCollectorInventory.JustPressed()) {
                if (Collector.GetCollectorMutex().IsLocked()) return;
                Collector.GetCollectorMutex().RequestLock(() => {
                    Collector.ShowCollectorInventory();
                });
            }
        }
    }

    public void OnDayStarted(object? sender, DayStartedEventArgs e) {
        collectorLocations.Clear();

        Utility.ForEachLocation(location => {
            foreach (var obj in location.objects.Pairs) {
                if (obj.Value.QualifiedItemId == CollectorID) {
                    collectorLocations.Add(obj.Value.Location);
                    break;
                }
            }

            return true;
        });

        if (collectorLocations.Count < 1) return;

        string output = "\nCollector Locations:\n";

        foreach (var location in collectorLocations) {
            output += $"{location.NameOrUniqueName}\n";
        }

        Log.Info(output, true);

        DelayedAction action = new DelayedAction(2000);
        action.behavior = () => {
            foreach (var location in collectorLocations) {
                Collector.DoCollection(location);
            }
        };
        Game1.delayedActions.Add(action);
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