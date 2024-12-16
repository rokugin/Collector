using StardewValley;
using SObject = StardewValley.Object;

namespace Collector.Patches;

public static class ObjectPatch {

    public static bool CheckForAction_Prefix(SObject __instance, Farmer who, bool justCheckingForActivity = false) {
        try {
            if (__instance.QualifiedItemId == ModEntry.CollectorID) {
                if (!justCheckingForActivity) {
                    Collector.TryOpenCollectorInventory();

                    return false;
                }
            }

            return true;
        } catch (Exception e) {
            Log.Error($"Exception in CheckForAction_Prefix:\n{e.Message}", true);
        }
        return true;
    }

}