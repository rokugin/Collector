namespace Collector;

internal class ModConfig {

    public bool Logging { get; set; } = false;

    public ModConfigKeys Controls { get; set; } = new();

    public bool HarvestCrops { get; set; } = true;
    public bool HarvestFlowers { get; set; } = true;
    public bool CollectForage { get; set; } = true;
    public bool CollectSpringOnions { get; set; } = true;
    public bool CollectGinger { get; set; } = true;
    public bool CollectTreeSeeds { get; set; } = true;
    public bool CollectMoss { get; set; } = true;
    public bool CollectFruitTrees { get; set; } = true;
    public bool CollectArtifactSpots { get; set; } = true;
    public bool CollectSeedSpots { get; set; } = true;
    public bool CollectMushroomBoxes { get; set; } = true;
    public bool SearchGarbageCans { get; set; } = true;
    public bool CollectBerryBushes { get; set; } = true;
    public bool CollectPanningSpots { get; set; } = true;
    public bool ChopSecretWoodsStumps { get; set; } = true;
    public bool CollectSlimeHutches { get; set; } = true;

}