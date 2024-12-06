using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Constants;
using StardewValley.Extensions;
using StardewValley.GameData.Crops;
using StardewValley.GameData.Locations;
using StardewValley.GameData.WildTrees;
using StardewValley.Internal;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace Collector;

public class Collector {

    List<SObject> collectors = new List<SObject>();
    IInventory logItems = new Inventory();

    const string CollectorID = "rokugin.collector_Collector";

    ModConfig config => ModEntry.Config;

    const int Farming = 0, Fishing = 1, Foraging = 2, Mining = 3, Combat = 4;
    const int Gatherer = 13, Botanist = 16;

    bool shakeTrees = true;
    bool harvestCrops = true;
    bool harvestFlowers = false;
    bool harvestSpringOnions = true;
    bool harvestGinger = true;

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
        string output = $"\n====={location.NameOrUniqueName}=====\n";
        logItems.Clear();
        Farmer randomPlayer = Game1.random.ChooseFrom(Game1.getOnlineFarmers().ToList());

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

                StackOrAdd(logItems, obj);
                CollectItem(obj);
                location.objects.Remove(obj.TileLocation);

                if (obj.isForage()) {
                    foreach (var farmer in Game1.getOnlineFarmers()) {
                        farmer.gainExperience(Foraging, gatherer ? 14 : 7);
                    }

                    Game1.stats.ItemsForaged++;
                }
            }

            if (obj.QualifiedItemId == "(O)590" || obj.QualifiedItemId == "(O)SeedSpot") {
                Random r = Utility.CreateDaySaveRandom((0f - obj.TileLocation.X) * 7f, obj.TileLocation.Y * 777f,
                    Game1.netWorldState.Value.TreasureTotemsUsed * 777);

                foreach (var farmer in Game1.getOnlineFarmers()) {
                    farmer.stats.Increment("ArtifactSpotsDug", 1);
                }

                if (randomPlayer.stats.Get("ArtifactSpotsDug") > 2 && r.NextDouble() < 0.008 +
                    (!randomPlayer.mailReceived.Contains("DefenseBookDropped") ? randomPlayer.stats.Get("ArtifactSpotsDug") * 0.002 : 0.005)) {
                    Item item = ItemRegistry.Create("(O)Book_Defense");
                    StackOrAdd(logItems, item);
                    CollectItem(item);
                }

                if (obj.QualifiedItemId == "(O)SeedSpot") {
                    Item raccoonSeedForCurrentTimeOfYear = Utility.getRaccoonSeedForCurrentTimeOfYear(Game1.player, r);
                    StackOrAdd(logItems, raccoonSeedForCurrentTimeOfYear);
                    CollectItem(raccoonSeedForCurrentTimeOfYear);
                } else {
                    CollectArtifactSpot(obj.TileLocation, randomPlayer, location);
                }

                location.objects.Remove(obj.TileLocation);

                foreach (var farmer in Game1.getOnlineFarmers()) {
                    farmer.gainExperience(Foraging, 15);
                }
            }
        }

        foreach (var feature in location.terrainFeatures.Values) {
            if (feature is HoeDirt soil && soil.crop != null) {
                HarvestCrop(soil.crop, soil);
            }

            if (feature is Tree tree && shakeTrees) {
                if (tree.hasSeed.Value && (Game1.IsMultiplayer || Game1.player.ForagingLevel >= 1)) {
                    bool dropDefaultSeed = true;
                    WildTreeData data = tree.GetData();

                    if (data != null && data.SeedDropItems?.Count > 0) {
                        foreach (var drop in data.SeedDropItems) {
                            Item item = tree.TryGetDrop(drop, Game1.random, Game1.player, "SeedDropItems");
                            if (item != null) {
                                bool botanist = false;
                                foreach (var farmer in Game1.getOnlineFarmers()) {
                                    if (botanist) break;
                                    botanist = farmer.professions.Contains(Botanist);
                                }

                                if (botanist && item.HasContextTag("forage_item")) {
                                    item.Quality = 4;
                                }

                                StackOrAdd(logItems, item);
                                CollectItem(item);

                                if (!drop.ContinueOnDrop) {
                                    dropDefaultSeed = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (dropDefaultSeed && data != null) {
                        Item item = ItemRegistry.Create(data.SeedItemId);

                        bool botanist = false;
                        foreach (var farmer in Game1.getOnlineFarmers()) {
                            if (botanist) break;
                            botanist = farmer.professions.Contains(Botanist);
                        }

                        if (botanist && item.HasContextTag("forage_item")) {
                            item.Quality = 4;
                        }

                        StackOrAdd(logItems, item);
                        CollectItem(item);

                        bool canSpawnGoldenMysteryBox = false;
                        foreach (var farmer in Game1.getOnlineFarmers()) {
                            if (canSpawnGoldenMysteryBox) break;
                            canSpawnGoldenMysteryBox = farmer.stats.Get(StatKeys.Mastery(2)) != 0;
                        }

                        if (Utility.tryRollMysteryBox(0.03)) {
                            item = ItemRegistry.Create(canSpawnGoldenMysteryBox ? "(O)GoldenMysteryBox" : "(O)MysteryBox");
                            StackOrAdd(logItems, item);
                            CollectItem(item);
                        }

                        TryCollectRareObject(randomPlayer);

                        if (Game1.random.NextBool() && Game1.player.team.SpecialOrderRuleActive("DROP_QI_BEANS")) {
                            item = ItemRegistry.Create("(O)890");
                            StackOrAdd(logItems, item);
                            CollectItem(item);
                        }

                        tree.hasSeed.Value = false;
                    }
                }

                if (tree.hasMoss.Value) {
                    Item item = Tree.CreateMossItem();
                    StackOrAdd(logItems, item);
                    CollectItem(item);

                    foreach (var farmer in Game1.getOnlineFarmers()) {
                        farmer.gainExperience(Foraging, item.Stack);
                    }

                    tree.hasMoss.Value = false;
                }
            }
            if (feature is FruitTree fruitTree && fruitTree.fruit.Count > 0) {
                int fruitQuality = fruitTree.GetQuality();

                for (int i = 0; i < fruitTree.fruit.Count; i++) {
                    Item item = fruitTree.fruit[i];
                    item.Quality = fruitQuality;
                    StackOrAdd(logItems, item);
                    CollectItem(item);
                    fruitTree.fruit[i] = null;
                }

                fruitTree.fruit.Clear();
            }
        }

        foreach (var item in logItems) {
            output += $"{item.Stack}x {item.DisplayName} collected.\n";
        }

        Log.Info(output, true);
    }

    void HarvestCrop(Crop crop, HoeDirt soil) {
        Farmer randomPlayer = Game1.random.ChooseFrom(Game1.getOnlineFarmers().ToList());

        if (crop.whichForageCrop.Value == "1" && harvestSpringOnions) {
            Random r = Utility.CreateDaySaveRandom(crop.tilePosition.X * 1000, crop.tilePosition.Y * 2000);
            Item item = ItemRegistry.Create("(O)399");
            bool botanist = false;

            foreach (var farmer in Game1.getOnlineFarmers()) {
                if (botanist) break;
                if (farmer.professions.Contains(Botanist)) {
                    botanist = true;
                }
            }

            if (botanist) {
                item.Quality = 4;
            } else if (r.NextDouble() < (double)((float)randomPlayer.ForagingLevel / 30f)) {
                item.Quality = 2;
            } else if (r.NextDouble() < (double)((float)randomPlayer.ForagingLevel / 15f)) {
                item.Quality = 1;
            }

            foreach (var farmer in Game1.getOnlineFarmers()) {
                farmer.gainExperience(Foraging, 3);
            }

            StackOrAdd(logItems, item.getOne());
            CollectItem(item.getOne());
            soil.destroyCrop(false);
        } else if (crop.whichForageCrop.Value == "2" && harvestGinger) {
            Item item = ItemRegistry.Create("(O)829");

            foreach (var farmer in Game1.getOnlineFarmers()) {
                farmer.gainExperience(Foraging, 7);
            }

            StackOrAdd(logItems, item.getOne());
            CollectItem(item.getOne());
            soil.destroyCrop(false);
        }
        if (harvestCrops && crop.currentPhase.Value >= crop.phaseDays.Count - 1 && (!crop.fullyGrown.Value || crop.dayOfCurrentPhase.Value <= 0)) {
            if (string.IsNullOrWhiteSpace(crop.indexOfHarvest.Value)) {
                return;
            }

            CropData data = crop.GetData();
            Random r = Utility.CreateRandom(crop.tilePosition.X * 7.0, crop.tilePosition.Y * 11.0, Game1.stats.DaysPlayed, Game1.uniqueIDForThisGame);
            int fertilizerQualityLevel = soil.GetFertilizerQualityBoostLevel();
            double chanceForGoldQuality = 0.2 * ((double)randomPlayer.FarmingLevel / 10.0) + 0.2 * (double)fertilizerQualityLevel * (((double)randomPlayer.FarmingLevel + 2.0) / 12.0) + 0.01;
            double chanceForSilverQuality = Math.Min(0.75, chanceForGoldQuality * 2.0);
            int cropQuality = 0;

            if (ItemRegistry.GetDataOrErrorItem(data.HarvestItemId).Category == -80 && !harvestFlowers) {
                return;
            }

            if (fertilizerQualityLevel >= 3 && r.NextDouble() < chanceForGoldQuality / 2.0) {
                cropQuality = 4;
            } else if (r.NextDouble() < chanceForGoldQuality) {
                cropQuality = 2;
            } else if (r.NextDouble() < chanceForSilverQuality || fertilizerQualityLevel >= 3) {
                cropQuality = 1;
            }

            cropQuality = MathHelper.Clamp(cropQuality, data?.HarvestMinQuality ?? 0, data?.HarvestMaxQuality ?? cropQuality);
            int numToHarvest = 1;

            if (data != null) {
                int minStack = data.HarvestMinStack;
                int maxStack = Math.Max(minStack, data.HarvestMaxStack);

                if (data.HarvestMaxIncreasePerFarmingLevel > 0f) {
                    maxStack += (int)((float)randomPlayer.FarmingLevel * data.HarvestMaxIncreasePerFarmingLevel);
                }

                if (minStack > 1 || maxStack > 1) {
                    numToHarvest = r.Next(minStack, maxStack + 1);
                }
            }

            if (data != null && data.ExtraHarvestChance > 0.0) {
                while (r.NextDouble() < Math.Min(0.9, data.ExtraHarvestChance)) {
                    numToHarvest++;
                }
            }

            Item harvestedItem = crop.programColored.Value ?
                new ColoredObject(crop.indexOfHarvest.Value, 1, crop.tintColor.Value) { Quality = cropQuality } :
                ItemRegistry.Create(crop.indexOfHarvest.Value, 1, cropQuality);

            if (crop.indexOfHarvest.Value == "421") {
                crop.indexOfHarvest.Value = "431";
                numToHarvest = r.Next(1, 4);
            }

            StackOrAdd(logItems, harvestedItem.getOne());
            CollectItem(harvestedItem.getOne());

            int price = 0;

            if (harvestedItem is SObject obj) {
                price = obj.Price;
            }

            float experience = (float)(16.0 * Math.Log(0.018 * (double)price + 1.0, Math.E));

            foreach (var farmer in Game1.getOnlineFarmers()) {
                farmer.gainExperience(Farming, (int)Math.Round(experience));
            }

            for (int i = 0; i < numToHarvest - 1; i++) {
                StackOrAdd(logItems, harvestedItem.getOne());
                CollectItem(harvestedItem.getOne());
            }

            if (crop.indexOfHarvest.Value == "771") {
                if (r.NextDouble() < 0.1) {
                    Item item = ItemRegistry.Create("(O)770");
                    StackOrAdd(logItems, item.getOne());
                    CollectItem(item.getOne());
                }
            }

            if (crop.indexOfHarvest.Value == "262" && r.NextDouble() < 0.4) {
                Item item = ItemRegistry.Create("(O)178");
                StackOrAdd(logItems, item.getOne());
                CollectItem(item.getOne());
            }

            int regrowDays = data?.RegrowDays ?? (-1);
            if (regrowDays <= 0) {
                soil.destroyCrop(false);
                return;
            }
            crop.fullyGrown.Value = true;
            if (crop.dayOfCurrentPhase.Value == regrowDays) {
                crop.updateDrawMath(crop.tilePosition);
            }
            crop.dayOfCurrentPhase.Value = regrowDays;
        }
    }

    void CollectArtifactSpot(Vector2 tile, Farmer farmer, GameLocation location) {
        bool generousEnchantment = false;
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
            StackOrAdd(logItems, item);
            CollectItem(item);
        }

        TryCollectRareObject(farmer, r);

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

            StackOrAdd(logItems, item);
            CollectItem(item);

            if (generousEnchantment && drop.ApplyGenerousEnchantment && r.NextBool()) {
                item = item.getOne();
                item = (Item)ItemQueryResolver.ApplyItemFields(item, drop, itemQueryContext);

                StackOrAdd(logItems, item);
                CollectItem(item);
            }

            if (!drop.ContinueOnDrop) break;
        }
    }

    void TryCollectRareObject(Farmer farmer, Random r = null!) {
        if (r == null) {
            r = Game1.random;
        }

        double chanceMod = 9;
        double dailyLuckWeight = 1;
        double luckMod = 1 + farmer.team.AverageDailyLuck() * dailyLuckWeight;

        if (farmer.stats.Get(StatKeys.Mastery(0)) != 0 && r.NextDouble() < 0.001 * chanceMod * luckMod) {
            Item item = ItemRegistry.Create("(O)GoldenAnimalCracker");
            StackOrAdd(logItems, item);
            CollectItem(item);
        }

        if (Game1.stats.DaysPlayed > 2 && r.NextDouble() < 0.002 * chanceMod) {
            Item item = Utility.getRandomCosmeticItem(r);
            StackOrAdd(logItems, item);
            CollectItem(item);
        }

        if (Game1.stats.DaysPlayed > 2 && r.NextDouble() < 0.0006 * chanceMod) {
            Item item = ItemRegistry.Create("(O)SkillBook_" + r.Next(5));
            StackOrAdd(logItems, item);
            CollectItem(item);
        }
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

    void CollectItem(Item item) {
        GetCollectorInventory().RemoveEmptySlots();
        StackOrAdd(GetCollectorInventory(), item);
    }

    void StackOrAdd(IInventory item_list, Item item) {
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