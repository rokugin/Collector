using HarmonyLib;
using StardewValley;
using SObject = StardewValley.Object;

namespace Collector.Patches;

public static class ObjectPatch {

    [HarmonyBefore(["2Retr0.PlacementPlus"])]
    public static bool CheckForAction_Prefix(SObject __instance, Farmer who, bool justCheckingForActivity = false) {
        try {
            if (!justCheckingForActivity && __instance.QualifiedItemId == ModEntry.CollectorID) {
                Collector.TryOpenCollectorInventory();

                return false;
            }

            return true;
        }
        catch (Exception e) {
            Log.Error($"Exception in CheckForAction_Prefix:\n{e.Message}");
        }
        return true;
    }

    public static bool PerformObjectDropInAction_Prefix(SObject __instance, Item dropInItem, bool probe, Farmer who, bool returnFalseIfItemConsumed = false) {
        try {
            if (!probe && __instance.QualifiedItemId == ModEntry.CollectorID) {
                Collector.TryOpenCollectorInventory();

                return false;
            }

            return true;
        }
        catch (Exception e) {
            Log.Error($"Exception in PerformObjectDropInAction_Prefix:\n{e.Message}");
        }
        return true;
    }

}