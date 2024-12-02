using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Network;
using SObject = StardewValley.Object;

namespace Collector;

public class Collector {

    List<SObject> collectors = new List<SObject>();

    const string CollectorID = "rokugin.collector_Collector";

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

        foreach (var obj in collectors) {
            foreach (var obj2 in obj.Location.objects.Pairs) {
                if (obj2.Value.HasTypeObject()) {
                    GetCollectorInventory().Add(obj2.Value);
                }
            }
        }
    }

    public void DoCollection(GameLocation location) {
        string output = "\n";
        foreach (var obj in location.objects.Values) {
            //output += obj.BaseName + "\n";
            if (obj.isForage() || obj.IsSpawnedObject && obj.questItem.Value == false) {
                output += obj.BaseName + " collected.\n";
                AddItem(obj);
                location.objects.Remove(obj.TileLocation);
            }
        }
        Log.Info(output, true);
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