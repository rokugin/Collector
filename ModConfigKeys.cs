using StardewModdingAPI.Utilities;
using StardewModdingAPI;

namespace Collector;

internal class ModConfigKeys {

    public KeybindList ActivateLocalCollector { get; set; } = new(SButton.PageUp);
    public KeybindList OpenCollectorInventory { get; set; } = new(SButton.PageDown);

}