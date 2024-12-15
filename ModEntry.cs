﻿using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

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
        I18n.Init(helper.Translation);

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
        if (!Config.CollectPanningSpots) return;

        DelayedAction action = new DelayedAction(500);
        action.behavior = () => {
            List<Item> items = new();
            string output = $"\n=====Panning Spots=====\n";

            if (!Config.AllowGlobalCollector) {
                foreach (var location in collectorLocations) {
                    items.AddRange(Collector.CollectPanningSpot(location));
                }
            } else {
                Utility.ForEachLocation(location => {
                    items.AddRange(Collector.CollectPanningSpot(location));

                    return true;
                });
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
            if (Config.AllowGlobalCollector && Keys.ActivateGlobalCollector.JustPressed()) {
                Utility.ForEachLocation(location => {
                    Collector.DoCollection(location);

                    return true;
                });
            }
            if (Config.OpenCollectorInventoryAnywhere && Keys.OpenCollectorInventory.JustPressed()) {
                Collector.TryOpenCollectorInventory();
            }
        }
    }

    public void OnDayStarted(object? sender, DayStartedEventArgs e) {
        collectorLocations.Clear();

        Utility.ForEachLocation(location => {
            foreach (var obj in location.objects.Pairs) {
                if (obj.Value.QualifiedItemId == CollectorID) {
                    if (!collectorLocations.Contains(obj.Value.Location)) {
                        collectorLocations.Add(obj.Value.Location);
                    }
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

        Log.Info(output);

        DelayedAction action = new DelayedAction(2000);
        action.behavior = () => {
            foreach (var location in collectorLocations) {
                Collector.DoCollection(location);
            }
        };
        Game1.delayedActions.Add(action);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e) {
        var cm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

        if (cm is null) return;
        
        cm.Register(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));

        cm.AddPageLink(ModManifest, "CollectorSettings", () => I18n.CollectorSettings());

        cm.AddSectionTitle(ModManifest, () => "Cheats");
        cm.AddBoolOption(ModManifest, () => Config.AllowGlobalCollector, v => Config.AllowGlobalCollector = v,
            () => I18n.GlobalCollector(), () => I18n.GlobalCollector_Desc());

        cm.AddKeybindList(ModManifest, () => Config.Controls.ActivateGlobalCollector, v => Config.Controls.ActivateGlobalCollector = v,
            () => I18n.GlobalCollector_Func(), () => I18n.GlobalCollector_FuncDesc());

        cm.AddBoolOption(ModManifest, () => Config.OpenCollectorInventoryAnywhere, v => Config.OpenCollectorInventoryAnywhere = v,
            () => I18n.InventoryAnywhere(), () => I18n.InventoryAnywhere_Desc());

        cm.AddKeybindList(ModManifest, () => Config.Controls.OpenCollectorInventory, v => Config.Controls.OpenCollectorInventory = v,
            () => I18n.InventoryAnywhere_Func(), () => I18n.InventoryAnywhere_FuncDesc());

        cm.AddSectionTitle(ModManifest, () => I18n.Debugging());
        cm.AddBoolOption(ModManifest, () => Config.Logging, v => Config.Logging = v,
            () => I18n.Logging(), () => I18n.Logging_Desc());

        cm.AddPage(ModManifest, "CollectorSettings", () => I18n.CollectorSettings());
        cm.AddBoolOption(ModManifest, () => Config.CollectArtifactSpots, v => Config.CollectArtifactSpots = v,
            () => I18n.ArtifactSpots(), () => I18n.ArtifactSpots_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectBerryBushes, v => Config.CollectBerryBushes = v,
            () => I18n.BerryBushes(), () => I18n.BerryBushes_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectCrabPots, v => Config.CollectCrabPots = v,
            () => I18n.CrabPots(), () => I18n.CrabPots_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectCrops, v => Config.CollectCrops = v,
            () => I18n.Crops(), () => I18n.Crops_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectFlowers, v => Config.CollectFlowers = v,
            () => I18n.Flowers(), () => I18n.Flowers_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectForage, v => Config.CollectForage = v,
            () => I18n.Forage(), () => I18n.Forage_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectFruitTrees, v => Config.CollectFruitTrees = v,
            () => I18n.FruitTrees(), () => I18n.FruitTrees_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectGarbageCans, v => Config.CollectGarbageCans = v,
            () => I18n.GarbageCans(), () => I18n.GarbageCans_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectGardenPots, v => Config.CollectGardenPots = v,
            () => I18n.GardenPots(), () => I18n.GardenPots_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectGinger, v => Config.CollectGinger = v,
            () => I18n.Ginger(), () => I18n.Ginger_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectMoss, v => Config.CollectMoss = v,
            () => I18n.Moss(), () => I18n.Moss_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectMushroomBoxes, v => Config.CollectMushroomBoxes = v,
            () => I18n.MushroomBoxes(), () => I18n.MushroomBoxes_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectPanningSpots, v => Config.CollectPanningSpots = v,
            () => I18n.PanningSpots(), () => I18n.PanningSpots_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectSecretWoodsStumps, v => Config.CollectSecretWoodsStumps = v,
            () => I18n.SecretWoodsStumps(), () => I18n.SecretWoodsStumps_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectSeedSpots, v => Config.CollectSeedSpots = v,
            () => I18n.SeedSpots(), () => I18n.SeedSpots_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectSlimeBalls, v => Config.CollectSlimeBalls = v,
            () => I18n.SlimeBalls(), () => I18n.SlimeBalls_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectSpawnedObjects, v => Config.CollectSpawnedObjects = v,
            () => I18n.SpawnedObjects(), () => I18n.SpawnedObjects_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectSpringOnions, v => Config.CollectSpringOnions = v,
            () => I18n.SpringOnions(), () => I18n.SpringOnions_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectTeaBushes, v => Config.CollectTeaBushes = v,
            () => I18n.TeaBushes(), () => I18n.TeaBushes_Desc());

        cm.AddBoolOption(ModManifest, () => Config.CollectTreeForage, v => Config.CollectTreeForage = v,
            () => I18n.TreeForage(), () => I18n.TreeForage_Desc());
    }

}