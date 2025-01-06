using StardewValley;
using StardewValley.SpecialOrders;

namespace Collector.Patches;

public static class SpecialOrderPatch {

    public static void IsTimedQuest_Postfix(ref bool __result, SpecialOrder __instance) {
        if (__instance.questKey.Value == "rokugin.collectorcp_Maru_0") { 
            __result = false;
        }
    }

    public static void GetDaysLeft_Postfix(ref int __result, SpecialOrder __instance) {
        if (__instance.questKey.Value == "rokugin.collectorcp_Maru_0") {
            __result = 100;
        }
    }

    public static void Update_Postfix(SpecialOrder __instance) {
        if (!Game1.player.mailReceived.Contains("rokugin.collectorcp_Maru_0_PrizeTicketDeferred")
            && __instance.questKey.Value == "rokugin.collectorcp_Maru_0" && __instance.questState.Value == SpecialOrderStatus.Complete) {
            Game1.player.stats.Decrement("specialOrderPrizeTickets");
            Game1.player.mailReceived.Add("rokugin.collectorcp_Maru_0_PrizeTicketDeferred");
        }
    }

}