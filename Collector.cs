using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Constants;
using StardewValley.Extensions;
using StardewValley.GameData.Crops;
using StardewValley.GameData.GarbageCans;
using StardewValley.GameData.Locations;
using StardewValley.GameData.WildTrees;
using StardewValley.Internal;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using xTile.Tiles;
using SObject = StardewValley.Object;
using xTile.ObjectModel;
using StardewValley.Locations;
using StardewValley.GameData.FarmAnimals;
using StardewValley.Tools;
using StardewValley.Enchantments;

namespace Collector;

public class Collector {

    public List<Item> itemsToCollect = new();
    public List<SObject> objectsToRemove = new();
    public List<ResourceClump> clumpsToRemove = new();
    public string yieldInfo = "";
    public int yieldCount = 0;

    public static string CollectorID => ModEntry.CollectorID;

    ModConfig config => ModEntry.Config;

    Farmer randomPlayer => Game1.random.ChooseFrom(Game1.getOnlineFarmers().ToList());

    const int Farming = 0, Fishing = 1, Foraging = 2, Mining = 3, Combat = 4;
    const int Forester = 12, Gatherer = 13, Botanist = 16;
    const string copperOre = "378", iridiumOre = "386", goldOre = "384", ironOre = "380", boneFragment = "881", coal = "382";
    const string diamond = "72", omniGeode = "749", fireQuartz = "82", frozenTear = "84", earthCrystal = "86";
    const string luckyRing = "859", artifactTrove = "(O)275", taroTuber = "(O)831", fossilizedTail = "(O)822";

    bool collectAnimalProduce => config.CollectAnimalProduce;
    bool collectForage => config.CollectForage;
    bool collectSpawnedObjects => config.CollectSpawnedObjects;
    bool collectTreeForage => config.CollectTreeForage;
    bool collectMoss => config.CollectMoss;
    bool collectCrops => config.CollectCrops;
    bool collectFlowers => config.CollectFlowers;
    bool collectSpringOnions => config.CollectSpringOnions;
    bool collectGinger => config.CollectGinger;
    bool collectMushroomBoxes => config.CollectMushroomBoxes;
    bool collectGarbageCans => config.CollectGarbageCans;
    bool collectBerryBushes => config.CollectBerryBushes;
    bool collectTeaBushes => config.CollectTeaBushes;
    bool collectFruitTrees => config.CollectFruitTrees;
    bool collectArtifactSpots => config.CollectArtifactSpots;
    bool collectSeedSpots => config.CollectSeedSpots;
    bool collectPanningSpots => config.CollectPanningSpots;
    bool collectSecretWoodsStumps => config.CollectSecretWoodsStumps;
    bool collectSlimeBalls => config.CollectSlimeBalls;
    bool collectGardenPots => config.CollectGardenPots;
    bool collectCrabPots => config.CollectCrabPots;

    /// <summary>
    /// Check if any online farmer has the Botanist profession.
    /// </summary>
    public bool AnyBotanist() {
        bool botanist = false;

        foreach (var farmer in Game1.getOnlineFarmers()) {
            if (botanist) break;
            botanist = farmer.professions.Contains(Botanist);
        }

        return botanist;
    }

    /// <summary>
    /// Checks what Quality a harvested spawned object should be.
    /// </summary>
    public int GetHarvestedSpawnedObjectQuality(bool isForage, Vector2 tile, Random r = null!) {
        if (r == null) Utility.CreateDaySaveRandom(tile.X, tile.Y * 777f);

        if (isForage) {
            if (AnyBotanist()) {
                return 4;
            } else {
                if (r.NextBool((float)randomPlayer.ForagingLevel / 30)) {
                    return 2;
                } else if (r.NextBool((float)randomPlayer.ForagingLevel / 15)) {
                    return 1;
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Gives all online farmers experience.
    /// </summary>
    /// <param name="which">The skill to gain experience in.</param>
    /// <param name="howMuch">How much experience to gain.</param>
    public void OnlineFarmersGainExperience(int which, int howMuch) {
        foreach (var farmer in Game1.getOnlineFarmers()) {
            farmer.gainExperience(which, howMuch);
        }
    }

    public void OnlineFarmersGainExperience(int which, int howMuch, Farmer[] exclusions) {
        foreach (var farmer in Game1.getOnlineFarmers()) {
            if (!exclusions.Contains(farmer)) {
                farmer.gainExperience(which, howMuch);
            }
        }
    }

    public List<Item> DoDebugInfo(List<Item> oldList, List<Item> newList) {
        foreach (var item in oldList) {
            newList.Remove(item);
        }

        return newList;
    }

    public string GetQualityName(int quality) {
        string name = "";
        switch (quality) {
            case 1:
                name = "Silver";
                break;
            case 2:
                name = "Gold";
                break;
            case 4:
                name = "Iridium";
                break;
        }
        return name;
    }

    public void DoCollection(GameLocation location) {
        yieldInfo = $"\n====={location.NameOrUniqueName}=====\n";
        itemsToCollect.Clear();
        objectsToRemove.Clear();
        clumpsToRemove.Clear();

        foreach (var obj in location.objects.Values) {
            CollectForageAndSpawnedObject(obj);
            CollectArtifactAndSeedSpots(obj);
            CollectMushroomBox(obj);
            CollectSlimeBall(obj);
            CollectCrabPot(obj);
            CollectGardenPot(obj);
        }

        foreach (var obj in objectsToRemove) {
            location.objects.Remove(obj.TileLocation);
        }

        foreach (var animal in location.animals.Values) {
            CollectAnimalProduce(animal);
        }

        foreach (var feature in location.terrainFeatures.Values) {
            if (feature is HoeDirt soil && soil.crop != null) {
                CollectCrop(soil.crop, soil);
            }
            CollectTreeForage(feature);
            CollectFruitTree(feature);
            CollectTeaBush(feature);
        }

        foreach (var feature in location.largeTerrainFeatures) {
            CollectBush(feature);
        }

        CollectGarbageCans(location);

        itemsToCollect.AddRange(CollectPanningSpot(location));

        if (location is Woods) {
            foreach (var clump in location.resourceClumps) {
                CollectSecretWoodsStump(location, clump);
            }

            foreach (var clump in clumpsToRemove) {
                location.resourceClumps.Remove(clump);
            }
        }

        foreach (var item in itemsToCollect) {
            AddItem(item);
        }

        if (yieldCount > 0) Log.Info(yieldInfo, config.CollectionLogging);
        yieldCount = 0;
    }

    public void AddYieldInfo(Item item, Vector2 tileLocation, string source) {
        yieldCount++;
        yieldInfo += $"{item.Stack}x {(item.Quality != 0 ? GetQualityName(item.Quality) + " " : "")}{item.DisplayName} collected " +
            $"from {source} at ({tileLocation.X}, {tileLocation.Y}).\n";
    }

    public void AddYieldInfo(Item item, Vector2 tileLocation) {
        yieldCount++;
        yieldInfo += $"{item.Stack}x {(item.Quality != 0 ? GetQualityName(item.Quality) + " " : "")}{item.DisplayName} collected " +
            $"from ({tileLocation.X}, {tileLocation.Y}).\n";
    }

    public void AddYieldInfo(Item item, string animalType, string animal) {
        yieldCount++;
        yieldInfo += $"{item.Stack}x {(item.Quality != 0 ? GetQualityName(item.Quality) + " " : "")}{item.DisplayName} collected " +
            $"from {animalType}: {animal}.\n";
    }

    public void AddYieldInfo(Item item, string garbageCanOwner) {
        yieldCount++;
        yieldInfo += $"{item.Stack}x {(item.Quality != 0 ? GetQualityName(item.Quality) + " " : "")}{item.DisplayName} collected " +
            $"from Garbage Can: {garbageCanOwner}.\n";
    }

    public void CollectAnimalProduce(FarmAnimal animal) {
        try {
            if (collectAnimalProduce) {
                if (animal.GetHarvestType().GetValueOrDefault() == FarmAnimalHarvestType.HarvestWithTool && animal.currentProduce.Value != null) {
                    Item item = ItemRegistry.Create("(O)" + animal.currentProduce.Value);
                    item.Quality = animal.produceQuality.Value;

                    if (animal.hasEatenAnimalCracker.Value) {
                        item.Stack = 2;
                    }

                    AddYieldInfo(item, animal.type.Value, animal.displayName);
                    itemsToCollect.Add(item);
                    animal.HandleStatsOnProduceCollected(item, (uint)item.Stack);
                    animal.currentProduce.Value = null;
                    animal.ReloadTextureIfNeeded();
                }
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in CollectAnimalProduce.\nError message:\n{e.Message}");
        }
    }

    public void CollectGarbageCans(GameLocation location) {
        try {
            if (collectGarbageCans) {
                Tile[,] buildingsArray = location.map.GetLayer("Buildings").Tiles.Array;
                foreach (var tile in buildingsArray) {
                    if (tile != null && tile.Properties.TryGetValue("Action", out PropertyValue? action) && action.ToString().StartsWith("Garbage")) {
                        string[] fields = ArgUtility.SplitBySpace(action);
                        string garbageCanOwner = fields[1];

                        if (Game1.netWorldState.Value.CheckedGarbage.Add(garbageCanOwner)) {
                            if (location.TryGetGarbageItem(garbageCanOwner, randomPlayer.DailyLuck, out Item item, out GarbageCanItemData data, out Random r)) {
                                AddYieldInfo(item, garbageCanOwner);
                                itemsToCollect.Add(item);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in CollectGarbageCans.\nError message:\n{e.Message}");
        }
    }

    public void CollectGardenPot(SObject obj) {
        try {
            if (collectGardenPots && obj is IndoorPot pot) {
                if (collectTeaBushes && pot.bush.Value is Bush bush && bush.readyForHarvest()) {
                    string shakeOff = bush.GetShakeOffItem();

                    if (shakeOff == null) return;

                    bush.tileSheetOffset.Value = 0;
                    bush.setUpSourceRect();
                    Item item = ItemRegistry.Create(shakeOff);
                    AddYieldInfo(item, pot.TileLocation, "Garden Pot");
                    itemsToCollect.Add(item);
                }

                if (pot.hoeDirt.Value.crop != null) {
                    CollectCrop(pot.hoeDirt.Value.crop, pot.hoeDirt.Value, true);
                }

                if (collectForage && pot.heldObject.Value != null) {
                    SObject o = pot.heldObject.Value;
                    o.Quality = pot.Location.GetHarvestSpawnedObjectQuality(randomPlayer, o.isForage(), pot.TileLocation);
                    AddYieldInfo(o, pot.TileLocation, "Garden Pot");
                    itemsToCollect.Add(o);
                    pot.heldObject.Value = null;
                    pot.readyForHarvest.Value = false;

                    foreach (var farmer in Game1.getOnlineFarmers()) {
                        pot.Location.OnHarvestedForage(farmer, o);
                    }
                }
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in CollectGardenPot.\nError message:\n{e.Message}");
        }
    }

    public void CollectCrabPot(SObject obj) {
        try {
            if (collectCrabPots && obj is CrabPot pot && pot.heldObject.Value != null) {
                Item item = pot.heldObject.Value;
                int number = item?.Stack ?? 1;

                if (Utility.CreateDaySaveRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed * 77,
                    obj.TileLocation.X * 777f + obj.TileLocation.Y).NextDouble() < 0.25 && randomPlayer.stats.Get("Book_Crabbing") != 0) {
                    number *= 2;
                }

                if (item != null) {
                    item.Stack = number;
                    pot.heldObject.Value = null;
                    AddYieldInfo(item, obj.TileLocation, "Crab Pot");
                    itemsToCollect.Add(item);

                    if (DataLoader.Fish(Game1.content).TryGetValue(item.ItemId, out var rawDataStr)) {
                        string[] rawData = rawDataStr.Split('/');
                        int minFishSize = (rawData.Length <= 5) ? 1 : Convert.ToInt32(rawData[5]);
                        int maxFishSize = (rawData.Length > 5) ? Convert.ToInt32(rawData[6]) : 10;
                        foreach (var farmer in Game1.getOnlineFarmers()) {
                            farmer.caughtFish(item.QualifiedItemId, Game1.random.Next(minFishSize, maxFishSize + 1), from_fish_pond: false, number);
                        }
                    }

                    OnlineFarmersGainExperience(Fishing, 5);

                }
                pot.readyForHarvest.Value = false;
                pot.tileIndexToShow = 710;
                pot.lidFlapping = true;
                pot.lidFlapTimer = 60f;
                pot.bait.Value = null;
                pot.shake = Vector2.Zero;
                pot.shakeTimer = 0f;
                //pot.ignoreRemovalTimer = 750; //Field is private, might need to set this with reflection
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in CollectCrabPot, Object: {obj.DisplayName}.\nError message:\n{e.Message}");
        }
    }

    public void CollectSlimeBall(SObject obj) {
        try {
            if (collectSlimeBalls && obj.QualifiedItemId == "(BC)56") {
                Random r = Utility.CreateRandom(Game1.stats.DaysPlayed, Game1.uniqueIDForThisGame, obj.TileLocation.X * 77.0, obj.TileLocation.Y * 777.0, 2.0);
                Item item = ItemRegistry.Create("(O)766", r.Next(10, 21));
                AddYieldInfo(item, obj.TileLocation, "Slime Ball");
                itemsToCollect.Add(item);

                while (r.NextDouble() < 0.33) {
                    item = ItemRegistry.Create("(O)557");
                    AddYieldInfo(item, obj.TileLocation, "Slime Ball");
                    itemsToCollect.Add(item);
                }

                objectsToRemove.Add(obj);
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in CollectSlimeBall, Object: {obj.DisplayName}.\nError message:\n{e.Message}");
        }
    }

    public void CollectSecretWoodsStump(GameLocation location, ResourceClump clump) {
        try {
            if (collectSecretWoodsStumps && clump.parentSheetIndex.Value == 600) {
                Item item = null!;
                int upgradeLevel = 1;
                bool shavingEnchantment = false;
                float power = Math.Max(1f, (upgradeLevel + 1) * 0.75f);

                int chops = (int)(clump.health.Value / power);
                if (shavingEnchantment) {
                    while (chops > 0) {
                        chops--;
                        if (Game1.random.NextDouble() <= (double)(power / 12f)) {
                            item = ItemRegistry.Create("(O)709");
                            AddYieldInfo(item, clump.Tile, "Stump");
                            itemsToCollect.Add(item);
                        }
                    }
                }

                Game1.stats.StumpsChopped++;
                OnlineFarmersGainExperience(Foraging, 25);
                int amount = 2;
                Random r;

                if (Game1.IsMultiplayer) {
                    Game1.recentMultiplayerRandom = Utility.CreateRandom(clump.Tile.X * 1000.0, clump.Tile.Y);
                    r = Game1.recentMultiplayerRandom;
                } else {
                    r = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed, clump.Tile.X * 7.0, clump.Tile.Y * 11.0);
                }

                bool forester = false;
                foreach (var farmer in Game1.getOnlineFarmers()) {
                    if (forester) break;
                    forester = farmer.professions.Contains(Forester) && r.NextBool();
                }

                if (forester) amount++;

                item = ItemRegistry.Create("(O)709", amount);
                AddYieldInfo(item, clump.Tile, "Stump");
                itemsToCollect.Add(item);

                if (r.NextDouble() < 0.1) {
                    item = ItemRegistry.Create("(O)292");
                    AddYieldInfo(item, clump.Tile, "Stump");
                    itemsToCollect.Add(item);
                }

                if (Game1.random.NextDouble() <= 0.25 && Game1.player.team.SpecialOrderRuleActive("DROP_QI_BEANS")) {
                    item = ItemRegistry.Create("(O)890");
                    AddYieldInfo(item, clump.Tile, "Stump");
                    itemsToCollect.Add(item);
                }

                clumpsToRemove.Add(clump);
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in CollectSecretWoodsStump.\nError message:\n{e.Message}");
        }
    }

    public List<Item> CollectPanningSpot(GameLocation location) {
        List<Item> items = new();
        if (collectPanningSpots) {
            Point panningSpot = location.orePanPoint.Value;

            if (panningSpot != Point.Zero) {
                items = GetPanItems(location, randomPlayer);

                foreach (Item item in items) {
                    AddYieldInfo(item, new Vector2(panningSpot.X, panningSpot.Y), "Panning Spot");
                }

                location.orePanPoint.Value = Point.Zero;
            }
        }

        return items;
    }
    
    public List<Item> GetPanItems(GameLocation location, Farmer who) {
        List<Item> items = new();
        try {
            string whichOre = copperOre;
            string whichExtra = null!;
            int upgradeLevel = 1;
            bool archaeologistEnchantment = ModEntry.Config.PanArchaeologistEnchantment;
            bool generousEnchantment = ModEntry.Config.PanGenerousEnchantment;
            bool fisherEnchantment = ModEntry.Config.FisherEnchantment;
            int enchantments = 0;
            int experience = 0;

            foreach (var farmer in Game1.getOnlineFarmers()) {
                farmer.stats.Increment("TimesPanned", 1);
            }

            Random r = Utility.CreateRandom(location.orePanPoint.X, location.orePanPoint.Y * 1000.0, Game1.stats.DaysPlayed, who.stats.Get("TimesPanned") * 77);
            double roll = r.NextDouble() - who.luckLevel.Value * 0.001 - who.DailyLuck;
            roll -= (upgradeLevel - 1) * 0.05;

            if (roll < 0.01) {
                whichOre = iridiumOre;
            } else if (roll < 0.241) {
                whichOre = goldOre;
            } else if (roll < 0.6) {
                whichOre = ironOre;
            }

            if (whichOre != iridiumOre && r.NextDouble() < 0.1 + (archaeologistEnchantment ? 0.1 : 0.0)) {
                whichOre = boneFragment;
            }

            int orePieces = r.Next(2, 7) + 1 + (int)((r.NextDouble() + 0.1 + (double)(who.luckLevel.Value / 10f) + who.DailyLuck) * 2.0);
            int extraPieces = r.Next(5) + 1 + (int)((r.NextDouble() + 0.1 + (double)(who.luckLevel.Value / 10f)) * 2.0);
            orePieces += upgradeLevel - 1;
            roll = r.NextDouble() - who.DailyLuck;
            int numRolls = upgradeLevel;
            bool gotRing = false;

            double extraChance = (upgradeLevel - 1) * 0.04;
            if (enchantments > 0) {
                extraChance *= 1.25;
            }

            if (generousEnchantment) {
                numRolls += 2;
            }
            
            while (r.NextDouble() - who.DailyLuck < 0.4 + who.LuckLevel * 0.04 + extraChance && numRolls > 0) {
                roll = r.NextDouble() - who.DailyLuck;
                roll -= (upgradeLevel - 1) * 0.005;

                whichExtra = coal;
                if (roll < 0.02 + who.LuckLevel * 0.002 && r.NextDouble() < 0.75) {
                    whichExtra = diamond;
                    extraPieces = 1;
                } else if (roll < 0.1 && r.NextDouble() < 0.75) {
                    whichExtra = (60 + r.Next(5) * 2).ToString();
                    extraPieces = 1;
                } else if (roll < 0.36) {
                    whichExtra = omniGeode;
                    extraPieces = Math.Max(1, extraPieces / 2);
                } else if (roll < 0.5) {
                    whichExtra = r.Choose(fireQuartz, frozenTear, earthCrystal);
                    extraPieces = 1;
                }
                
                if (roll < who.LuckLevel * 0.002 && !gotRing && r.NextDouble() < 0.33) {
                    items.Add(new Ring(luckyRing));
                    gotRing = true;
                    experience++;
                }

                if (roll < 0.01 && r.NextDouble() < 0.5) {
                    items.Add(Utility.getRandomCosmeticItem(r));
                    experience++;
                }

                if (r.NextDouble() < 0.1 && fisherEnchantment) {
                    Item f = location.getFish(1f, null, r.Next(1, 6), who, 0.0, who.Tile);
                    if (f != null && f.Category == -4) {
                        items.Add(f);
                        experience++;
                    }
                }

                if (r.NextDouble() < 0.02 + (archaeologistEnchantment ? 0.05 : 0.0)) {
                    Item artifact = location.tryGetRandomArtifactFromThisLocation(who, r);
                    if (artifact != null) {
                        items.Add(artifact);
                        experience++;
                    }
                }

                if (Utility.tryRollMysteryBox(0.05, r)) {
                    items.Add(ItemRegistry.Create((who.stats.Get(StatKeys.Mastery(2)) != 0) ? "(O)GoldenMysteryBox" : "(O)MysteryBox"));
                    experience++;
                }

                if (whichExtra != null) {
                    items.Add(new SObject(whichExtra, extraPieces));
                    experience++;
                }
                numRolls--;
            }

            int amount = 0;
            while (r.NextDouble() < 0.05 + (archaeologistEnchantment ? 0.15 : 0.0)) {
                amount++;
            }
            
            if (amount > 0) {
                items.Add(ItemRegistry.Create(artifactTrove, amount));
                experience++;
            }

            items.Add(new SObject(whichOre, orePieces));
            experience++;

            if (!(location is IslandNorth islandNorth)) {
                if (location is IslandLocation && r.NextDouble() < 0.2) {
                    items.Add(ItemRegistry.Create(taroTuber, r.Next(2, 6)));
                    experience++;
                }
            } else if (islandNorth.bridgeFixed.Value && r.NextDouble() < 0.2) {
                items.Add(ItemRegistry.Create(fossilizedTail));
                experience++;
            }

            OnlineFarmersGainExperience(Mining, orePieces + extraPieces);
            OnlineFarmersGainExperience(Foraging, experience * 7);

            return items;
        }
        catch (Exception e) {
            Log.Error($"\nException in GetPanItems.\nError message:\n{e.Message}");
        }
        return items;
    }

    public void CollectTeaBush(TerrainFeature feature) {
        try {
            if (collectTeaBushes && feature is Bush bush && bush.size.Value == 3 && bush.tileSheetOffset.Value == 1) {
                if (ModEntry.CB is not null && ModEntry.CB.TryGetCustomBush(bush, out var customBush) && customBush != null &&
                    ModEntry.CB.TryGetShakeOffItem(bush, out Item? item) && item != null) {
                    AddYieldInfo(item, feature.Tile, customBush.DisplayName + " Bush");
                    itemsToCollect.Add(item);
                    bush.tileSheetOffset.Value = 0;
                    bush.setUpSourceRect();
                } else {
                    string shakeOff = bush.GetShakeOffItem();

                    if (shakeOff == null) return;
                    
                    bush.tileSheetOffset.Value = 0;
                    bush.setUpSourceRect();
                    item = ItemRegistry.Create(shakeOff);
                    AddYieldInfo(item, feature.Tile, "Green Tea Bush");
                    itemsToCollect.Add(item);
                }
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in CollectTeaBush.\nError message:\n{e.Message}");
        }
    }

    public void CollectBush(LargeTerrainFeature feature) {
        try {
            if (collectBerryBushes && feature is Bush bush && bush.size.Value == 1) {
                if (ModEntry.BBM is not null) {
                    string itemID = ModEntry.BBM.FakeShake(bush);

                    if (itemID == null) return;

                    Item item = ItemRegistry.Create(itemID);
                    int number = 1 + randomPlayer.ForagingLevel / 4;
                    item.Stack = number;

                    if (AnyBotanist()) item.Quality = 4;
                    AddYieldInfo(item, feature.Tile, "Berry Bush");
                    itemsToCollect.Add(item);
                    OnlineFarmersGainExperience(Foraging, number);
                } else {
                    string shakeOff = bush.GetShakeOffItem();

                    if (shakeOff == null) return;

                    bush.tileSheetOffset.Value = 0;
                    bush.setUpSourceRect();
                    int number = 1 + randomPlayer.ForagingLevel / 4;
                    Item item = ItemRegistry.Create(shakeOff).getOne();
                    item.Stack = number;

                    if (AnyBotanist()) item.Quality = 4;

                    AddYieldInfo(item, feature.Tile, "Berry Bush");
                    itemsToCollect.Add(item);
                    OnlineFarmersGainExperience(Foraging, number);
                }
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in CollectBush.\nError message:\n{e.Message}");
        }
    }

    public void CollectFruitTree(TerrainFeature feature) {
        try {
            if (collectFruitTrees && feature is FruitTree fruitTree && fruitTree.fruit.Count > 0) {
                int fruitQuality = fruitTree.GetQuality();

                for (int i = 0; i < fruitTree.fruit.Count; i++) {
                    Item item = fruitTree.fruit[i];
                    item.Quality = fruitQuality;
                    AddYieldInfo(item, feature.Tile, "Fruit Tree");
                    itemsToCollect.Add(item);
                    fruitTree.fruit[i] = null;
                }

                fruitTree.fruit.Clear();
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in CollectFruitTree.\nError message:\n{e.Message}");
        }
    }

    public void CollectTreeForage(TerrainFeature feature) {
        try {
            if (feature is Tree tree) {
                if (collectTreeForage && (Game1.IsMultiplayer || Game1.player.ForagingLevel >= 1)) {
                    bool dropDefaultSeed = true;
                    WildTreeData data = tree.GetData();

                    if (tree.hasSeed.Value && data != null && data.SeedDropItems?.Count > 0) {
                        foreach (var drop in data.SeedDropItems) {
                            Item item = tree.TryGetDrop(drop, Game1.random, Game1.player, "SeedDropItems");
                            if (item != null) {
                                if (AnyBotanist() && item.HasContextTag("forage_item")) {
                                    item.Quality = 4;
                                }

                                AddYieldInfo(item, feature.Tile, "Tree");
                                itemsToCollect.Add(item);

                                if (!drop.ContinueOnDrop) {
                                    dropDefaultSeed = false;
                                    tree.hasSeed.Value = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (data != null && data.ShakeItems?.Count > 0) {
                        foreach (var drop in data.ShakeItems) {
                            Item item = tree.TryGetDrop(drop, Game1.random, Game1.player, "ShakeItems");
                            if (item != null) {
                                if (AnyBotanist() && item.HasContextTag("forage_item")) {
                                    item.Quality = 4;
                                }

                                AddYieldInfo(item, feature.Tile, "Tree");
                                itemsToCollect.Add(item);
                            }
                        }
                    }

                    if (tree.hasSeed.Value && dropDefaultSeed && data != null) {
                        Item item = ItemRegistry.Create(data.SeedItemId);

                        if (AnyBotanist() && item.HasContextTag("forage_item")) {
                            item.Quality = 4;
                        }

                        AddYieldInfo(item, feature.Tile, "Tree");
                        itemsToCollect.Add(item);

                        bool canSpawnGoldenMysteryBox = false;
                        foreach (var farmer in Game1.getOnlineFarmers()) {
                            if (canSpawnGoldenMysteryBox) break;
                            canSpawnGoldenMysteryBox = farmer.stats.Get(StatKeys.Mastery(2)) != 0;
                        }

                        if (Utility.tryRollMysteryBox(0.03)) {
                            item = ItemRegistry.Create(canSpawnGoldenMysteryBox ? "(O)GoldenMysteryBox" : "(O)MysteryBox");
                            AddYieldInfo(item, feature.Tile, "Tree");
                            itemsToCollect.Add(item);
                        }

                        TryCollectRareObject(feature.Tile, randomPlayer);

                        if (Game1.random.NextBool() && Game1.player.team.SpecialOrderRuleActive("DROP_QI_BEANS")) {
                            item = ItemRegistry.Create("(O)890");
                            AddYieldInfo(item, feature.Tile, "Tree");
                            itemsToCollect.Add(item);
                        }

                        tree.hasSeed.Value = false;
                    }

                    tree.wasShakenToday.Value = true;
                }

                if (collectMoss && tree.hasMoss.Value) {
                    Item item = Tree.CreateMossItem();
                    itemsToCollect.Add(item);
                    OnlineFarmersGainExperience(Foraging, item.Stack);
                    tree.hasMoss.Value = false;
                }
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in CollectTreeForage.\nError message:\n{e.Message}");
        }
    }

    public void CollectForageAndSpawnedObject(SObject obj) {
        if (obj.isForage() && !collectForage) return;
        if (!obj.isForage() && !collectSpawnedObjects) return;

        try {
            if ((obj.isForage() || obj.IsSpawnedObject) && obj.questItem.Value == false) {
                Random r = Utility.CreateDaySaveRandom(obj.TileLocation.X, obj.TileLocation.Y * 777f);
                Item item = ItemRegistry.Create(obj.QualifiedItemId);
                int quality = GetHarvestedSpawnedObjectQuality(obj.isForage(), obj.TileLocation, r);
                item.Quality = obj.isForage() ? quality : obj.Quality;

                if (obj.isForage()) {
                    bool gatherer = false;
                    foreach (var farmer in Game1.getOnlineFarmers()) {
                        if (gatherer) break;
                        gatherer = farmer.professions.Contains(Gatherer) && r.NextDouble() < 0.2;
                    }
                    if (gatherer) item.Stack = 2;

                    if (obj.SpecialVariable == 724519) {
                        OnlineFarmersGainExperience(Foraging, 2);
                        OnlineFarmersGainExperience(Farming, 3);
                    } else {
                        OnlineFarmersGainExperience(Foraging, 7);
                    }

                    if (gatherer) OnlineFarmersGainExperience(Foraging, 7);

                    Game1.stats.ItemsForaged++;
                } else {
                    item.Stack = obj.Stack;
                }

                AddYieldInfo(item, obj.TileLocation);
                itemsToCollect.Add(item);
                objectsToRemove.Add(obj);
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in CollectForageAndSpawnedObject, Object: {obj.DisplayName}.\nError message:\n{e.Message}");
        }
    }

    public void CollectArtifactAndSeedSpots(SObject obj) {
        try {
            if ((collectArtifactSpots && obj.QualifiedItemId == "(O)590") || (collectSeedSpots && obj.QualifiedItemId == "(O)SeedSpot")) {
                Random r = Utility.CreateDaySaveRandom((0f - obj.TileLocation.X) * 7f, obj.TileLocation.Y * 777f,
                        Game1.netWorldState.Value.TreasureTotemsUsed * 777);

                foreach (var farmer in Game1.getOnlineFarmers()) {
                    farmer.stats.Increment("ArtifactSpotsDug", 1);
                }

                Farmer cachedRandomPlayer = randomPlayer;
                if (cachedRandomPlayer.stats.Get("ArtifactSpotsDug") > 2 && r.NextDouble() < 0.008 +
                        (!cachedRandomPlayer.mailReceived.Contains("DefenseBookDropped") ? cachedRandomPlayer.stats.Get("ArtifactSpotsDug") * 0.002 : 0.005)) {
                    cachedRandomPlayer.mailReceived.Add("DefenseBookDropped");
                    Item item = ItemRegistry.Create("(O)Book_Defense");
                    AddYieldInfo(item, obj.TileLocation, obj.QualifiedItemId == "(O)SeedSpot" ? "Seed Spot" : "Artifact Spot");
                    itemsToCollect.Add(item);
                }

                if (collectSeedSpots && obj.QualifiedItemId == "(O)SeedSpot") {
                    Item item = Utility.getRaccoonSeedForCurrentTimeOfYear(Game1.player, r);
                    AddYieldInfo(item, obj.TileLocation, "Seed Spot");
                    itemsToCollect.Add(item);
                } else if (collectArtifactSpots) {
                    CollectArtifactSpot(obj.TileLocation, randomPlayer, obj.Location);
                }

                objectsToRemove.Add(obj);

                OnlineFarmersGainExperience(Foraging, 15);
            }
        }
        catch (Exception e) {
            Log.Error($"Exception in CollectArtifactAndSeedSpots, Object: {obj.DisplayName}.\nError message:\n{e.Message}");
        }
    }

    public void CollectMushroomBox(SObject obj) {
        try {
            if (collectMushroomBoxes && obj.QualifiedItemId == "(BC)128" && obj.readyForHarvest.Value) {
                if (obj.heldObject.Value == null) return;

                AddYieldInfo(obj.heldObject.Value, obj.TileLocation, "Mushroom Box");
                itemsToCollect.Add(obj.heldObject.Value);
                OnlineFarmersGainExperience(Foraging, 5);
                obj.heldObject.Value = null;
                obj.readyForHarvest.Value = false;
                obj.showNextIndex.Value = false;
                obj.ResetParentSheetIndex();
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in CollectMushroomBox, Object: {obj.DisplayName}.\nError message:\n{e.Message}");
        }
    }

    public void CollectCrop(Crop crop, HoeDirt soil, bool potCrop = false) {
        try {
            CropData data = crop.GetData();

            if (crop.whichForageCrop.Value == "1" && collectSpringOnions) {
                Random r = Utility.CreateDaySaveRandom(crop.tilePosition.X * 1000, crop.tilePosition.Y * 2000);
                Item item = ItemRegistry.Create("(O)399");
                bool botanist = AnyBotanist();

                if (botanist) {
                    item.Quality = 4;
                } else if (r.NextDouble() < (double)(randomPlayer.ForagingLevel / 30f)) {
                    item.Quality = 2;
                } else if (r.NextDouble() < (double)(randomPlayer.ForagingLevel / 15f)) {
                    item.Quality = 1;
                }

                OnlineFarmersGainExperience(Foraging, 3);
                AddYieldInfo(item, crop.tilePosition);
                itemsToCollect.Add(item);
                soil.destroyCrop(false);
            } else if (crop.whichForageCrop.Value == "2" && collectGinger) {
                Item item = ItemRegistry.Create("(O)829");
                OnlineFarmersGainExperience(Foraging, 7);
                AddYieldInfo(item, crop.tilePosition);
                itemsToCollect.Add(item);
                soil.destroyCrop(false);
            }
            if (data == null) return;
            bool isFlower = ItemRegistry.GetDataOrErrorItem(data.HarvestItemId).Category == -80;
            if (crop.currentPhase.Value >= crop.phaseDays.Count - 1 && (!crop.fullyGrown.Value || crop.dayOfCurrentPhase.Value <= 0)) {
                if (string.IsNullOrWhiteSpace(crop.indexOfHarvest.Value)) {
                    return;
                }

                if ((isFlower && !collectFlowers) || (!isFlower && !collectCrops)) {
                    return;
                }

                Random r = Utility.CreateRandom(crop.tilePosition.X * 7.0, crop.tilePosition.Y * 11.0, Game1.stats.DaysPlayed, Game1.uniqueIDForThisGame);
                int fertilizerQualityLevel = soil.GetFertilizerQualityBoostLevel();
                double chanceForGoldQuality = 0.2 * (randomPlayer.FarmingLevel / 10.0) + 0.2 * fertilizerQualityLevel *
                    ((randomPlayer.FarmingLevel + 2.0) / 12.0) + 0.01;
                double chanceForSilverQuality = Math.Min(0.75, chanceForGoldQuality * 2.0);
                int cropQuality = 0;

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
                        maxStack += (int)(randomPlayer.FarmingLevel * data.HarvestMaxIncreasePerFarmingLevel);
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

                harvestedItem.Stack = numToHarvest;
                AddYieldInfo(harvestedItem, crop.tilePosition, "Crop");
                itemsToCollect.Add(harvestedItem);
                int price = 0;

                if (harvestedItem is SObject obj) {
                    price = obj.Price;
                }

                float experience = (float)(16.0 * Math.Log(0.018 * price + 1.0, Math.E));

                OnlineFarmersGainExperience(Farming, (int)Math.Round(experience));

                if (crop.indexOfHarvest.Value == "771") {
                    if (r.NextDouble() < 0.1) {
                        Item item = ItemRegistry.Create("(O)770");
                        AddYieldInfo(item, crop.tilePosition, "Crop");
                        itemsToCollect.Add(item);
                    }
                }

                if (crop.indexOfHarvest.Value == "262" && r.NextDouble() < 0.4) {
                    Item item = ItemRegistry.Create("(O)178");
                    AddYieldInfo(item, crop.tilePosition, "Crop");
                    itemsToCollect.Add(item);
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
        catch (Exception e) {
            Log.Error($"\nException in CollectCrop.\nError message:\n{e.Message}");
        }
    }

    public void CollectArtifactSpot(Vector2 tile, Farmer farmer, GameLocation location) {
        try {
            Random r = Utility.CreateDaySaveRandom(tile.X * 2000, tile.Y, Game1.netWorldState.Value.TreasureTotemsUsed * 777);
            LocationData locData = location.GetData();
            Farmer fakeFarmer = new Farmer();
            fakeFarmer.CurrentTool = new Hoe();
            fakeFarmer.CurrentTool.AddEnchantment(new ArchaeologistEnchantment());
            ItemQueryContext itemQueryContext = new ItemQueryContext(location, farmer, r, "location '" + location.NameOrUniqueName + "' > artifact spots");
            ItemQueryContext altItemQueryContext =
                        new ItemQueryContext(location, fakeFarmer, r, "location '" + location.NameOrUniqueName + "' > artifact spots");
            IEnumerable<ArtifactSpotDropData> possibleDrops = Game1.locationData["Default"].ArtifactSpots;
            bool generousEnchantment = ModEntry.Config.HoeGenerousEnchantment;
            bool archaeologistEnchantment = ModEntry.Config.HoeArchaeologistEnchantment;

            if (locData != null && locData.ArtifactSpots?.Count > 0) {
                possibleDrops = possibleDrops.Concat(locData.ArtifactSpots);
            }

            possibleDrops = possibleDrops.OrderBy((ArtifactSpotDropData p) => p.Precedence);

            if (Game1.player.mailReceived.Contains("sawQiPlane") && r.NextDouble() < 0.05 + Game1.player.team.AverageDailyLuck() / 2.0) {
                Item item = ItemRegistry.Create("(O)MysteryBox", r.Next(1, 3));
                AddYieldInfo(item, tile, "Artifact Spot");
                itemsToCollect.Add(item);
            }

            TryCollectRareObject(tile, farmer, r);

            foreach (var drop in possibleDrops) {
                if (!r.NextBool(drop.Chance) || (drop.Condition != null && !GameStateQuery.CheckConditions(drop.Condition, location, farmer, null, null, r))) {
                    continue;
                }

                Item item = null!;

                if (drop.Id == "RANDOM_ARTIFACT_FOR_DIG_SPOT" && archaeologistEnchantment) {
                    item = ItemQueryResolver.TryResolveRandomItem(drop, altItemQueryContext, avoidRepeat: false, null, null, null,
                        delegate (string query, string error) {
                            Log.Error($"Location '{location.NameOrUniqueName}' failed parsing item query '{query}' for artifact spot '{drop.Id}': {error}");
                        });
                } else {
                    item = ItemQueryResolver.TryResolveRandomItem(drop, itemQueryContext, avoidRepeat: false, null, null, null,
                        delegate (string query, string error) {
                            Log.Error($"Location '{location.NameOrUniqueName}' failed parsing item query '{query}' for artifact spot '{drop.Id}': {error}");
                        });
                }

                if (item == null || (Game1.netWorldState.Value.LostBooksFound >= 21 && item.QualifiedItemId == "(O)102")) {
                    continue;
                }

                AddYieldInfo(item, tile, "Artifact Spot");
                itemsToCollect.Add(item);

                if (generousEnchantment && drop.ApplyGenerousEnchantment && r.NextBool()) {
                    item = item.getOne();
                    item = (Item)ItemQueryResolver.ApplyItemFields(item, drop, itemQueryContext);
                    AddYieldInfo(item, tile, "Artifact Spot");
                    itemsToCollect.Add(item);
                }

                if (!drop.ContinueOnDrop) break;
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in CollectArtifactSpot.\nError message:\n{e.Message}");
        }
    }

    public void TryCollectRareObject(Vector2 tile, Farmer farmer, Random r = null!) {
        try {
            if (r == null) {
                r = Game1.random;
            }

            double chanceMod = 9;
            double dailyLuckWeight = 1;
            double luckMod = 1 + farmer.team.AverageDailyLuck() * dailyLuckWeight;

            if (farmer.stats.Get(StatKeys.Mastery(0)) != 0 && r.NextDouble() < 0.001 * chanceMod * luckMod) {
                Item item = ItemRegistry.Create("(O)GoldenAnimalCracker");
                AddYieldInfo(item, tile);
                itemsToCollect.Add(item);
            }

            if (Game1.stats.DaysPlayed > 2 && r.NextDouble() < 0.002 * chanceMod) {
                Item item = Utility.getRandomCosmeticItem(r);
                AddYieldInfo(item, tile);
                itemsToCollect.Add(item);
            }

            if (Game1.stats.DaysPlayed > 2 && r.NextDouble() < 0.0006 * chanceMod) {
                Item item = ItemRegistry.Create("(O)SkillBook_" + r.Next(5));
                AddYieldInfo(item, tile);
                itemsToCollect.Add(item);
            }
        }
        catch (Exception e) {
            Log.Error($"\nException in TryCollectRareObject.\nError message:\n{e.Message}");
        }
    }

    public static void TryOpenCollectorInventory() {
        GetCollectorMutex().RequestLock(() => { ShowCollectorInventory(); }, () => { Game1.showRedMessage("In use by another player."); });
    }

    public static void ShowCollectorInventory() {
        Game1.activeClickableMenu = new CollectorItemGrabMenu(inventory: GetCollectorIInventory(),
                    reverseGrab: false,
                    showReceivingMenu: true,
                    highlightFunction: InventoryMenu.highlightAllItems,
                    behaviorOnItemSelectFunction: GrabItemFromInventory,
                    message: null!,
                    behaviorOnItemGrab: GrabItemFromChest,
                    snapToBottom: false,
                    canBeExitedWithKey: true,
                    playRightClickSound: true,
                    allowRightClick: true,
                    showOrganizeButton: true,
                    source: 1,
                    sourceItem: null,
                    whichSpecialButton: -1,
                    context: "rokugin.collector",
                    allowExitWithHeldItem: true);
    }

    public static void GrabItemFromInventory(Item item, Farmer farmer) {
        if (item.Stack == 0) item.Stack = 1;

        Item tmp = AddItem(item);
        farmer.removeItemFromInventory(item);

        GetCollectorIInventory().RemoveEmptySlots();

        int oldID = ((Game1.activeClickableMenu.currentlySnappedComponent != null) ? Game1.activeClickableMenu.currentlySnappedComponent.myID : (-1));
        ShowCollectorInventory();
        (Game1.activeClickableMenu as ItemGrabMenu)!.heldItem = tmp;

        if (oldID != -1) {
            Game1.activeClickableMenu.currentlySnappedComponent = Game1.activeClickableMenu.getComponentWithID(oldID);
            Game1.activeClickableMenu.snapCursorToCurrentSnappedComponent();
        }
    }

    public static Item AddItem(Item item) {
        item.resetState();
        GetCollectorIInventory().RemoveEmptySlots();
        IInventory item_list = GetCollectorIInventory();

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

    public static void GrabItemFromChest(Item item, Farmer farmer) {
        if (farmer.couldInventoryAcceptThisItem(item)) {
            GetCollectorIInventory().Remove(item);
            GetCollectorIInventory().RemoveEmptySlots();
            ShowCollectorInventory();
        }
    }

    public static IInventory GetCollectorIInventory() {
        return Game1.player.team.GetOrCreateGlobalInventory(CollectorID);
    }

    public static Inventory GetCollectorInventory() {
        return Game1.player.team.GetOrCreateGlobalInventory(CollectorID);
    }

    public static NetMutex GetCollectorMutex() {
        return Game1.player.team.GetOrCreateGlobalInventoryMutex(CollectorID);
    }

}