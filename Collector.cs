using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Constants;
using StardewValley.Extensions;
using StardewValley.GameData.Locations;
using StardewValley.Internal;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Network;
using System;
using SObject = StardewValley.Object;

namespace Collector;

public class Collector {

    List<SObject> collectors = new List<SObject>();

    const string CollectorID = "rokugin.collector_Collector";

    ModConfig config => ModEntry.Config;

    const int Farming = 0, Fishing = 1, Foraging = 2, Mining = 3, Combat = 4;
    const int Gatherer = 13;

    public void OnDayStarted(object? sender, DayStartedEventArgs e) {
        collectors.Clear();

        Utility.ForEachLocation(location => {
            foreach (var obj in location.objects.Pairs) {
                if (obj.Value == (SObject)ItemRegistry.Create(CollectorID)) {
                    collectors.Add(obj.Value);
                }
            }

            return true;
        });

        if (collectors.Count < 1) return;


    }

    public void DoCollection(GameLocation location) {
        string output = "\n";
        foreach (var obj in location.objects.Values) {
            if (obj.isForage() || obj.IsSpawnedObject && obj.questItem.Value == false) {
                Random r = Utility.CreateDaySaveRandom(obj.TileLocation.X, obj.TileLocation.Y * 777f);
                bool gatherer = false;
                int quality = location.GetHarvestSpawnedObjectQuality(Game1.player, obj.isForage(), obj.TileLocation, r);
                obj.Quality = quality;

                if (obj.isForage()) {
                    foreach (var farmer in Game1.getOnlineFarmers()) {
                        if (gatherer == true) break;
                        if (farmer.professions.Contains(Gatherer)) {
                            if (r.NextDouble() < 0.2) {
                                gatherer = true;
                                obj.Stack = 2;
                            }
                        }
                    }
                }


                CollectItem(obj);
                location.objects.Remove(obj.TileLocation);
                output += $"{obj.Stack}x " + obj.BaseName + " collected.\n";

                if (obj.isForage()) {
                    foreach (var farmer in Game1.getOnlineFarmers()) {
                        farmer.gainExperience(Foraging, gatherer ? 14 : 7);
                    }
                }
            }

            if (obj.QualifiedItemId == "(O)590" || obj.QualifiedItemId == "(O)SeedSpot") {
                Random r = Utility.CreateDaySaveRandom((0f - obj.TileLocation.X) * 7f, obj.TileLocation.Y * 777f,
                    Game1.netWorldState.Value.TreasureTotemsUsed * 777);

                foreach (var farmer in Game1.getOnlineFarmers()) {
                    farmer.stats.Increment("ArtifactSpotsDug", 1);
                }

                Farmer randomPlayer = r.ChooseFrom(Game1.getOnlineFarmers().ToList());
                if (randomPlayer.stats.Get("ArtifactSpotsDug") > 2 && r.NextDouble() < 0.008 +
                    (!randomPlayer.mailReceived.Contains("DefenseBookDropped") ? randomPlayer.stats.Get("ArtifactSpotsDug") * 0.002 : 0.005)) {
                    CollectItem(ItemRegistry.Create("(O)Book_Defense"));
                    output += $"1x Jack Be Nimble, Jack Be Thick collected.\n";
                }

                string artifactsCollected = null!;
                if (obj.QualifiedItemId == "(O)SeedSpot") {
                    Item raccoonSeedForCurrentTimeOfYear = Utility.getRaccoonSeedForCurrentTimeOfYear(Game1.player, r);
                    CollectItem(raccoonSeedForCurrentTimeOfYear);
                    output += $"{raccoonSeedForCurrentTimeOfYear.Stack}x " + raccoonSeedForCurrentTimeOfYear.BaseName + " collected.\n";
                } else {
                    artifactsCollected = CollectArtifactSpot(obj.TileLocation, randomPlayer, location);
                }
                output += artifactsCollected;

                location.objects.Remove(obj.TileLocation);

                foreach (var farmer in Game1.getOnlineFarmers()) {
                    farmer.gainExperience(Foraging, 15);
                }
            }
        }
        if (output.Length > 1) Log.Info(output, true);
    }

    string CollectArtifactSpot(Vector2 tile, Farmer farmer, GameLocation location) {
        bool generousEnchantment = false;
        string logOutput = null!;
        Random r = Utility.CreateDaySaveRandom(tile.X * 2000, tile.Y, Game1.netWorldState.Value.TreasureTotemsUsed * 777);
        LocationData locData = location.GetData();
        ItemQueryContext itemQueryContext = new ItemQueryContext(location, farmer, r, "location '" + location.NameOrUniqueName + "' > artifact spots");
        IEnumerable<ArtifactSpotDropData> possibleDrops = Game1.locationData["Default"].ArtifactSpots;
        if (locData != null && locData.ArtifactSpots?.Count > 0) {
            possibleDrops = possibleDrops.Concat(locData.ArtifactSpots);
        }
        possibleDrops = possibleDrops.OrderBy((ArtifactSpotDropData p) => p.Precedence);

        if (Game1.player.mailReceived.Contains("sawQiPlane") && r.NextDouble() < 0.05 + Game1.player.team.AverageDailyLuck() / 2.0) {
            Item item = ItemRegistry.Create("(O)MysteryBox", r.Next(1, 3));
            CollectItem(item);
            logOutput += $"{item.Stack}x " + item.BaseName + " collected.\n";
        }

        double chanceMod = 9;
        double dailyLuckWeight = 1;
        double luckMod = 1 + farmer.team.AverageDailyLuck() * dailyLuckWeight;

        if (farmer.stats.Get(StatKeys.Mastery(0)) != 0 && r.NextDouble() < 0.001 * chanceMod * luckMod) {
            Item item = ItemRegistry.Create("(O)GoldenAnimalCracker");
            CollectItem(item);
            logOutput += $"{item.Stack}x " + item.BaseName + " collected.\n";
        }
        if (Game1.stats.DaysPlayed > 2 && r.NextDouble() < 0.002 * chanceMod) {
            Item item = Utility.getRandomCosmeticItem(r);
            CollectItem(item);
            logOutput += $"{item.Stack}x " + item.BaseName + " collected.\n";
        }
        if (Game1.stats.DaysPlayed > 2 && r.NextDouble() < 0.0006 * chanceMod) {
            Item item = ItemRegistry.Create("(O)SkillBook_" + r.Next(5));
            CollectItem(item);
            logOutput += $"{item.Stack}x " + item.BaseName + " collected.\n";
        }

        foreach (var drop in possibleDrops) {
            if (!r.NextBool(drop.Chance) || (drop.Condition != null && !GameStateQuery.CheckConditions(drop.Condition, location, farmer, null, null, r))) {
                continue;
            }

            Item item = ItemQueryResolver.TryResolveRandomItem(drop, itemQueryContext, avoidRepeat: false, null, null, null, delegate (string query, string error) {
                Log.Error($"Location '{location.NameOrUniqueName}' failed parsing item query '{query}' for artifact spot '{drop.Id}': {error}");
            });
            if (item == null) {
                continue;
            }

            CollectItem(item);
            logOutput += $"{item.Stack}x " + item.BaseName + " collected.\n";

            if (generousEnchantment && drop.ApplyGenerousEnchantment && r.NextBool()) {
                item = item.getOne();
                item = (Item)ItemQueryResolver.ApplyItemFields(item, drop, itemQueryContext);

                CollectItem(item);
                logOutput += $"{item.Stack}x " + item.BaseName + " collected.\n";
            }

            if (!drop.ContinueOnDrop) break;
        }

        return logOutput;
    }

    public void ShowCollectorInventory() {
        Game1.activeClickableMenu = new ItemGrabMenu(GetCollectorInventory(),
                    false, true, InventoryMenu.highlightAllItems, GrabItemFromInventory, null, GrabItemFromChest, false,
                    true, true, true, true, 1, null, -1, "rokugin.collector", allowExitWithHeldItem: true);
    }

    void GrabItemFromInventory(Item item, Farmer farmer) {
        if (item.Stack == 0) item.Stack = 1;

        Item tmp = AddItem(item);
        farmer.removeItemFromInventory(item);

        GetCollectorInventory().RemoveEmptySlots();

        int oldID = ((Game1.activeClickableMenu.currentlySnappedComponent != null) ? Game1.activeClickableMenu.currentlySnappedComponent.myID : (-1));
        ShowCollectorInventory();
        (Game1.activeClickableMenu as ItemGrabMenu).heldItem = tmp;
        if (oldID != -1) {
            Game1.activeClickableMenu.currentlySnappedComponent = Game1.activeClickableMenu.getComponentWithID(oldID);
            Game1.activeClickableMenu.snapCursorToCurrentSnappedComponent();
        }
    }


    Item AddItem(Item item) {
        item.resetState();
        GetCollectorInventory().RemoveEmptySlots();
        IInventory item_list = GetCollectorInventory();

        for (int i = 0; i < item_list.Count; i++) {
            if (item_list[i] != null && item_list[i].canStackWith(item)) {
                int toRemove = item.Stack - item_list[i].addToStack(item);
                if (item.ConsumeStack(toRemove) == null) {
                    return null!;
                }
            }
        }

        item_list.Add(item);
        return null!;
    }

    void CollectItem(Item item, int amount = -1, int quality = -1) {
        if (amount != -1) item.Stack = amount;
        if (quality != -1) item.Quality = quality;
        GetCollectorInventory().RemoveEmptySlots();
        IInventory item_list = GetCollectorInventory();

        for (int i = 0; i < item_list.Count; i++) {
            if (item_list[i] != null && item_list[i].canStackWith(item)) {
                item_list[i].addToStack(item);
                return;
            }
        }

        item_list.Add(item);
    }

    void GrabItemFromChest(Item item, Farmer farmer) {
        if (farmer.couldInventoryAcceptThisItem(item)) {
            GetCollectorInventory().Remove(item);
            GetCollectorInventory().RemoveEmptySlots();
            ShowCollectorInventory();
        }
    }

    IInventory GetCollectorInventory() {
        return Game1.player.team.GetOrCreateGlobalInventory(CollectorID);
    }

    public static NetMutex GetCollectorMutex() {
        return Game1.player.team.GetOrCreateGlobalInventoryMutex(CollectorID);
    }

}