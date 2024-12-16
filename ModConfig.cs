namespace Collector;

internal class ModConfig {

    public bool AllLogging { get; set; } = false;
    public bool CollectionLogging { get; set; } = false;
    public bool PanningLogging { get; set; } = false;
    public bool InformationalLogging { get; set; } = false;

    public ModConfigKeys Controls { get; set; } = new();

    public bool GrabberRecipes { get; set; } = false;

    public bool CollectCrops { get; set; } = true;
    public bool CollectFlowers { get; set; } = false;
    public bool CollectForage { get; set; } = true;
    public bool CollectSpawnedObjects { get; set; } = true;
    public bool CollectSpringOnions { get; set; } = true;
    public bool CollectGinger { get; set; } = true;
    public bool CollectTreeForage { get; set; } = true;
    public bool CollectMoss { get; set; } = false;
    public bool CollectFruitTrees { get; set; } = true;
    public bool CollectArtifactSpots { get; set; } = true;
    public bool CollectSeedSpots { get; set; } = true;
    public bool CollectMushroomBoxes { get; set; } = true;
    public bool CollectGarbageCans { get; set; } = true;
    public bool CollectBerryBushes { get; set; } = true;
    public bool CollectTeaBushes { get; set; } = true;
    public bool CollectPanningSpots { get; set; } = true;
    public bool CollectSecretWoodsStumps { get; set; } = true;
    public bool CollectSlimeBalls { get; set; } = true;
    public bool CollectGardenPots { get; set; } = true;
    public bool CollectCrabPots { get; set; } = true;

    public bool AllowGlobalCollector { get; set; } = false;
    public bool OpenCollectorInventoryAnywhere { get; set; } = false;

}