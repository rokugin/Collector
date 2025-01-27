using Collector.Integration;
using Collector.Patches;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.SpecialOrders;
using System.Reflection;
using SObject = StardewValley.Object;

namespace Collector;

internal class ModEntry : Mod {
    public static IBushBloomModApi BBM = null!;
    public static ICustomBushApi CB = null!;
    public static IProfessionsApi WoL = null!;

    public static IMonitor SMonitor = null!;
    public static ModConfig Config = null!;
    public static IModHelper ModHelper = null!;
    public static ModConfigKeys Keys => Config.Controls;

    public static string CollectorID = "(BC)rokugin.collectorcp_Collector";

    Collector Collector = new();
    List<GameLocation> collectorLocations = new();

    public override void Entry(IModHelper helper) {
        Config = helper.ReadConfig<ModConfig>();
        ModHelper = helper;
        SMonitor = Monitor;
        I18n.Init(helper.Translation);

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        helper.Events.World.ObjectListChanged += OnObjectListChanged;
        helper.Events.GameLoop.DayEnding += OnDayEnding;

        var harmony = new Harmony(ModManifest.UniqueID);
        // patch to allow right clicking Collector to open inventory
        harmony.Patch(original: AccessTools.Method(typeof(SObject), nameof(SObject.checkForAction)),
            prefix: new HarmonyMethod(typeof(ObjectPatch), nameof(ObjectPatch.CheckForAction_Prefix)));
        // patch to allow right clicking Collector when holding an Object to open inventory
        harmony.Patch(original: AccessTools.Method(typeof(SObject), nameof(SObject.performObjectDropInAction)),
            prefix: new HarmonyMethod(typeof(ObjectPatch), nameof(ObjectPatch.PerformObjectDropInAction_Prefix)));
        // patch to remove special order timer in quest log
        harmony.Patch(original: AccessTools.Method(typeof(SpecialOrder), nameof(SpecialOrder.IsTimedQuest)),
            postfix: new HarmonyMethod(typeof(SpecialOrderPatch), nameof(SpecialOrderPatch.IsTimedQuest_Postfix)));
        // patch to make special order time never decrease
        harmony.Patch(original: AccessTools.Method(typeof(SpecialOrder), nameof(SpecialOrder.GetDaysLeft)),
            postfix: new HarmonyMethod(typeof(SpecialOrderPatch), nameof(SpecialOrderPatch.GetDaysLeft_Postfix)));
        // patch to keep special order from rewarding prize tickets
        harmony.Patch(original: AccessTools.Method(typeof(SpecialOrder), nameof(SpecialOrder.Update)),
            postfix: new HarmonyMethod(typeof(SpecialOrderPatch), nameof(SpecialOrderPatch.Update_Postfix)));
    }

    private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e) {
        if (!Context.IsMainPlayer) return;
        // when adding a new object to a location, if it's a Collector check if the location is already on the list and add it if not
        // perform collection function if location is added to list
        foreach (var pair in e.Added) {
            if (pair.Value.QualifiedItemId == CollectorID) {
                if (!collectorLocations.Contains(e.Location)) {
                    collectorLocations.Add(e.Location);
                    DelayedAction action = new DelayedAction(500);
                    action.behavior = () => {
                        Collector.DoCollection(e.Location);
                    };
                    Game1.delayedActions.Add(action);
                    Log.Info($"\n{e.Location.NameOrUniqueName} added to Collector Locations.\n", Config.InformationalLogging);
                    return;
                }
            }

            if (pair.Value.QualifiedItemId == ItemRegistry.GetDataOrErrorItem("(O)430").QualifiedItemId) {
                if (collectorLocations.Contains(e.Location)) {
                    DelayedAction action = new DelayedAction(500);
                    action.behavior = () => {
                        Collector.DoCollection(e.Location);
                    };
                    Game1.delayedActions.Add(action);
                }
            }
        }
        // when removing an object from a location, if it's a Collector check if there are any other Collectors in the location
        // remove the location from the list if the last Collector is removed from the location
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
                    Log.Info($"\n{e.Location.NameOrUniqueName} removed from Collector Locations.\n", Config.InformationalLogging);
                    return;
                }
            }
        }
    }

    private void OnTimeChanged(object? sender, TimeChangedEventArgs e) {
        if (!Context.IsMainPlayer) return;
        if (!Config.CollectPanningSpots) return;
        // perform panning spot collection function in either every location or each location in the list
        DelayedAction action = new DelayedAction(500);
        action.behavior = () => {
            List<Item> items = new();
            Collector.yieldInfo = $"\n=====Panning Spots=====\n";

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

            foreach (var item in items) {
                Collector.AddItem(item);
            }

            if (Collector.yieldCount > 0) Log.Info(Collector.yieldInfo, Config.PanningLogging);
            Collector.yieldCount = 0;
        };
        Game1.delayedActions.Add(action);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e) {
        // release mutex lock if nobody has the inventory open
        if (Context.IsWorldReady) {
            if (Collector.GetCollectorMutex().IsLocked() && Collector.GetCollectorMutex().IsLockHeld() && Game1.activeClickableMenu == null) {
                Collector.GetCollectorMutex().ReleaseLock();
            }
        }
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e) {
        if (Context.IsPlayerFree) {
            if (Context.IsMainPlayer && Config.AllowGlobalCollector && Keys.ActivateGlobalCollector.JustPressed()) {
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
        if (!Context.IsMainPlayer) return;

        collectorLocations.Clear();
        // iterate every location, add location to list if it has a Collector
        Utility.ForEachLocation(location => {
            foreach (var obj in location.objects.Pairs) {
                if (obj.Value.QualifiedItemId == CollectorID) {
                    if (!collectorLocations.Contains(location)) {
                        collectorLocations.Add(location);
                    }
                    break;
                }
            }

            return true;
        });

        if (Config.AllowGlobalCollector && Config.DailyGlobalCollector) {
            DelayedAction delayedAction = new(2000);
            delayedAction.behavior = () => {
                Utility.ForEachLocation(location => {
                    Collector.DoCollection(location);

                    return true;
                });
            };

            return;
        }

        if (collectorLocations.Count < 1) return;

        string output = "\nCollector Locations:\n";

        foreach (var location in collectorLocations) {
            output += $"{location.NameOrUniqueName}\n";
        }

        Log.Info(output, Config.InformationalLogging);

        if (!Config.RunOnDayStart) return;
        // iterate list of locations and perform collection in each location
        DelayedAction action = new(2000);
        action.behavior = () => {
            foreach (var location in collectorLocations) {
                Collector.DoCollection(location);
            }
        };
        Game1.delayedActions.Add(action);
    }

    private void OnDayEnding(object? sender, DayEndingEventArgs e) {
        if (!Context.IsMainPlayer || !Config.RunOnDayEnd || collectorLocations.Count < 1) return;

        foreach (var location in collectorLocations) {
            Collector.DoCollection(location);
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e) {
        if (!Helper.ModRegistry.IsLoaded("rokugin.collectorcp")) {
            Log.Warn("\nCP component missing, please check your installation.\nWithout CP component, mod will only function if cheats are enabled.\n");
        }

        RegisterContentPatcherTokens();

        BBM = Helper.ModRegistry.GetApi<IBushBloomModApi>("NCarigon.BushBloomMod")!;
        CB = Helper.ModRegistry.GetApi<ICustomBushApi>("furyx639.CustomBush")!;
        WoL = Helper.ModRegistry.GetApi<IProfessionsApi>("DaLion.Professions")!;

        var cm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

        if (cm is null) return;

        cm.Register(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));

        cm.AddPageLink(ModManifest, "GeneralSettings", I18n.GeneralSettings);
        cm.AddPageLink(ModManifest, "CollectorSettings", I18n.CollectorSettings, I18n.CollectorSettings_Desc);
        cm.AddPageLink(ModManifest, "Cheats", I18n.Cheats);
        cm.AddPageLink(ModManifest, "Debugging", I18n.Debugging);

        cm.AddPage(ModManifest, "GeneralSettings", I18n.GeneralSettings);
        cm.AddBoolOption(ModManifest, () => Config.RunOnDayStart, v => Config.RunOnDayStart = v, I18n.RunDayStart, I18n.RunDayStart_Desc);
        cm.AddBoolOption(ModManifest, () => Config.RunOnDayEnd, v => Config.RunOnDayEnd = v, I18n.RunDayEnd, I18n.RunDayEnd_Desc);
        cm.AddParagraph(ModManifest, I18n.GeneralSettingsInfo);
        cm.AddBoolOption(ModManifest, () => Config.SpecialOrders, v => Config.SpecialOrders = v, I18n.SpecialOrders, I18n.SpecialOrders_Desc);
        cm.AddBoolOption(ModManifest, () => Config.GrabberRecipes, v => Config.GrabberRecipes = v, I18n.GrabberRecipes, I18n.GrabberRecipes_Desc);

        cm.AddPage(ModManifest, "Cheats", I18n.Cheats);
        cm.AddBoolOption(ModManifest, () => Config.AllowGlobalCollector, v => Config.AllowGlobalCollector = v,
            I18n.GlobalCollector, I18n.GlobalCollector_Desc);
        cm.AddBoolOption(ModManifest, () => Config.DailyGlobalCollector, v => Config.DailyGlobalCollector = v,
            I18n.DailyGlobal, I18n.DailyGlobal_Desc);
        cm.AddKeybindList(ModManifest, () => Config.Controls.ActivateGlobalCollector, v => Config.Controls.ActivateGlobalCollector = v,
            I18n.GlobalCollector_Func, I18n.GlobalCollector_FuncDesc);
        cm.AddBoolOption(ModManifest, () => Config.OpenCollectorInventoryAnywhere, v => Config.OpenCollectorInventoryAnywhere = v,
            I18n.InventoryAnywhere, I18n.InventoryAnywhere_Desc);
        cm.AddKeybindList(ModManifest, () => Config.Controls.OpenCollectorInventory, v => Config.Controls.OpenCollectorInventory = v,
            I18n.InventoryAnywhere_Func, I18n.InventoryAnywhere_FuncDesc);
        cm.AddSectionTitle(ModManifest, I18n.Enchantments);
        cm.AddParagraph(ModManifest, () => " ");
        cm.AddSectionTitle(ModManifest, I18n.AxeSection);
        cm.AddBoolOption(ModManifest, () => Config.ShavingEnchantment, v => Config.ShavingEnchantment = v,
            I18n.ShavingEnchant, I18n.ShavingEnchant_Desc);
        cm.AddNumberOption(ModManifest, () => Config.AxeUpgradeLevel, v => Config.AxeUpgradeLevel = v,
            I18n.AxeUpgradeLevel, I18n.AxeUpgradeLevel_Desc, 1, interval: 1);
        cm.AddSectionTitle(ModManifest, I18n.PanSection);
        cm.AddBoolOption(ModManifest, () => Config.FisherEnchantment, v => Config.FisherEnchantment = v,
            I18n.FisherEnchant, I18n.FisherEnchant_Desc);
        cm.AddBoolOption(ModManifest, () => Config.PanArchaeologistEnchantment, v => Config.PanArchaeologistEnchantment = v,
            I18n.PanArchEnchant, I18n.PanArchEnchant_Desc);
        cm.AddBoolOption(ModManifest, () => Config.PanGenerousEnchantment, v => Config.PanGenerousEnchantment = v,
            I18n.PanGenerousEnchant, I18n.PanGenerousEnchant_Desc);
        cm.AddNumberOption(ModManifest, () => Config.PanUpgradeLevel, v => Config.PanUpgradeLevel = v,
            I18n.PanUpgradeLevel, I18n.PanUpgradeLevel_Desc, 1, interval: 1);
        cm.AddSectionTitle(ModManifest, I18n.HoeSection);
        cm.AddBoolOption(ModManifest, () => Config.HoeArchaeologistEnchantment, v => Config.HoeArchaeologistEnchantment = v,
            I18n.HoeArchEnchant, I18n.HoeArchEnchant_Desc);
        cm.AddBoolOption(ModManifest, () => Config.HoeGenerousEnchantment, v => Config.HoeGenerousEnchantment = v,
            I18n.HoeGenerousEnchant, I18n.HoeGenerousEnchant_Desc);

        cm.AddPage(ModManifest, "Debugging", I18n.Debugging);
        cm.AddBoolOption(ModManifest, () => Config.AllLogging, v => Config.AllLogging = v, I18n.AllLogging, I18n.AllLogging_Desc);
        cm.AddBoolOption(ModManifest, () => Config.InformationalLogging, v => Config.InformationalLogging = v,
            I18n.InformationalLogging, I18n.InformationalLogging_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectionLogging, v => Config.CollectionLogging = v,
            I18n.CollectionLogging, I18n.CollectionLogging_Desc);
        cm.AddBoolOption(ModManifest, () => Config.PanningLogging, v => Config.PanningLogging = v, I18n.PanningLogging, I18n.PanningLogging_Desc);
        cm.AddBoolOption(ModManifest, () => Config.VerboseLogging, v => Config.VerboseLogging = v, I18n.VerboseLogging, I18n.VerboseLogging_Desc);

        cm.AddPage(ModManifest, "CollectorSettings", I18n.CollectorSettings);
        cm.AddBoolOption(ModManifest, () => Config.CollectAnimalProduce, v => Config.CollectAnimalProduce = v,
            I18n.AnimalProduce, I18n.AnimalProduce_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectArtifactSpots, v => Config.CollectArtifactSpots = v,
            I18n.ArtifactSpots, I18n.ArtifactSpots_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectBerryBushes, v => Config.CollectBerryBushes = v, I18n.BerryBushes, I18n.BerryBushes_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectCrabPots, v => Config.CollectCrabPots = v, I18n.CrabPots, I18n.CrabPots_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectCrops, v => Config.CollectCrops = v, I18n.Crops, I18n.Crops_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectFlowers, v => Config.CollectFlowers = v, I18n.Flowers, I18n.Flowers_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectForage, v => Config.CollectForage = v, I18n.Forage, I18n.Forage_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectFruitTrees, v => Config.CollectFruitTrees = v, I18n.FruitTrees, I18n.FruitTrees_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectGarbageCans, v => Config.CollectGarbageCans = v, I18n.GarbageCans, I18n.GarbageCans_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectGardenPots, v => Config.CollectGardenPots = v, I18n.GardenPots, I18n.GardenPots_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectGinger, v => Config.CollectGinger = v, I18n.Ginger, I18n.Ginger_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectMoss, v => Config.CollectMoss = v, I18n.Moss, I18n.Moss_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectMushroomBoxes, v => Config.CollectMushroomBoxes = v,
            I18n.MushroomBoxes, I18n.MushroomBoxes_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectPanningSpots, v => Config.CollectPanningSpots = v, I18n.PanningSpots, I18n.PanningSpots_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectSecretWoodsStumps, v => Config.CollectSecretWoodsStumps = v,
            I18n.SecretWoodsStumps, I18n.SecretWoodsStumps_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectSeedSpots, v => Config.CollectSeedSpots = v, I18n.SeedSpots, I18n.SeedSpots_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectSlimeBalls, v => Config.CollectSlimeBalls = v, I18n.SlimeBalls, I18n.SlimeBalls_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectSpawnedObjects, v => Config.CollectSpawnedObjects = v,
            I18n.SpawnedObjects, I18n.SpawnedObjects_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectSpringOnions, v => Config.CollectSpringOnions = v, I18n.SpringOnions, I18n.SpringOnions_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectTeaBushes, v => Config.CollectTeaBushes = v, I18n.TeaBushes, I18n.TeaBushes_Desc);
        cm.AddBoolOption(ModManifest, () => Config.CollectTreeForage, v => Config.CollectTreeForage = v, I18n.TreeForage, I18n.TreeForage_Desc);

        string? version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        Log.Info($"\nFinished setting up Collector v{version}\n", true);
    }

    void RegisterContentPatcherTokens() {
        var api = Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");

        api?.RegisterToken(ModManifest, "GrabberRecipes", () => { return Config.GrabberRecipes ? ["true"] : ["false"]; });

        api?.RegisterToken(ModManifest, "SpecialOrders", () => { return Config.SpecialOrders ? ["true"] : ["false"]; });
    }

}