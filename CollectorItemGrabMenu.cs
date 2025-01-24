using StardewValley;
using StardewValley.Menus;
using Microsoft.Xna.Framework;

namespace Collector;

public class CollectorItemGrabMenu : ItemGrabMenu {

    public CollectorItemGrabMenu(IList<Item> inventory, bool reverseGrab, bool showReceivingMenu, InventoryMenu.highlightThisItem highlightFunction,
        behaviorOnItemSelect behaviorOnItemSelectFunction, string message, behaviorOnItemSelect? behaviorOnItemGrab = null,
        bool snapToBottom = false, bool canBeExitedWithKey = false, bool playRightClickSound = true, bool allowRightClick = true,
        bool showOrganizeButton = false, int source = 0, Item? sourceItem = null, int whichSpecialButton = -1, object? context = null,
        ItemExitBehavior heldItemExitBehavior = ItemExitBehavior.ReturnToPlayer, bool allowExitWithHeldItem = false)
        : base(inventory, reverseGrab, showReceivingMenu, highlightFunction, behaviorOnItemSelectFunction, message, behaviorOnItemGrab, snapToBottom,
            canBeExitedWithKey, playRightClickSound, allowRightClick, showOrganizeButton, source, sourceItem, whichSpecialButton, context,
            heldItemExitBehavior, allowExitWithHeldItem) {
        this.source = source;
        this.message = message;
        this.reverseGrab = reverseGrab;
        this.showReceivingMenu = showReceivingMenu;
        this.playRightClickSound = playRightClickSound;
        this.allowRightClick = allowRightClick;
        base.inventory.showGrayedOutSlots = true;
        this.sourceItem = sourceItem;
        this.whichSpecialButton = whichSpecialButton;
        this.context = context;

        if (sourceItem != null && Game1.currentLocation.objects.Values.Contains(sourceItem)) {
            _sourceItemInCurrentLocation = true;
        } else {
            _sourceItemInCurrentLocation = false;
        }

        int capacity = 70;
        int rows = 5;
        int containerWidth = 64 * (capacity / rows);
        ItemsToGrabMenu = new InventoryMenu(Game1.uiViewport.Width / 2 - containerWidth / 2,
            yPositionOnScreen + ((capacity < 70) ? 64 : (-21)), playerInventory: false, inventory, highlightFunction, capacity, rows);
        yPositionOnScreen += 42;
        base.inventory.SetPosition(base.inventory.xPositionOnScreen, base.inventory.yPositionOnScreen + 38 + 4);
        ItemsToGrabMenu.SetPosition(ItemsToGrabMenu.xPositionOnScreen - 32 + 8, ItemsToGrabMenu.yPositionOnScreen);
        storageSpaceTopBorderOffset = 20;
        trashCan.bounds.X = ItemsToGrabMenu.width + ItemsToGrabMenu.xPositionOnScreen + borderWidth * 2;
        okButton.bounds.X = ItemsToGrabMenu.width + ItemsToGrabMenu.xPositionOnScreen + borderWidth * 2;
        ItemsToGrabMenu.populateClickableComponentList();

        for (int i = 0; i < ItemsToGrabMenu.inventory.Count; i++) {
            if (ItemsToGrabMenu.inventory[i] != null) {
                ItemsToGrabMenu.inventory[i].myID += 53910;
                ItemsToGrabMenu.inventory[i].upNeighborID += 53910;
                ItemsToGrabMenu.inventory[i].rightNeighborID += 53910;
                ItemsToGrabMenu.inventory[i].downNeighborID = -7777;
                ItemsToGrabMenu.inventory[i].leftNeighborID += 53910;
                ItemsToGrabMenu.inventory[i].fullyImmutable = true;
            }
        }

        behaviorFunction = behaviorOnItemSelectFunction;
        this.behaviorOnItemGrab = behaviorOnItemGrab;
        canExitOnKey = canBeExitedWithKey;

        if (showOrganizeButton) {
            fillStacksButton = new ClickableTextureComponent("", new Rectangle(xPositionOnScreen + width,
                yPositionOnScreen + height / 3 - 64 - 64 - 16, 64, 64), "", Game1.content.LoadString("Strings\\UI:ItemGrab_FillStacks"),
                Game1.mouseCursors, new Rectangle(103, 469, 16, 16), 4f) {
                myID = 12952,
                upNeighborID = ((colorPickerToggleButton != null) ? 27346 : ((specialButton != null) ? 12485 : (-500))),
                downNeighborID = 106,
                leftNeighborID = 53921,
                region = 15923
            };
            organizeButton = new ClickableTextureComponent("", new Rectangle(xPositionOnScreen + width,
                yPositionOnScreen + height / 3 - 64, 64, 64), "", Game1.content.LoadString("Strings\\UI:ItemGrab_Organize"),
                Game1.mouseCursors, new Rectangle(162, 440, 16, 16), 4f) {
                myID = 106,
                upNeighborID = 12952,
                downNeighborID = 5948,
                leftNeighborID = 53921,
                region = 15923
            };
        }

        RepositionSideButtons();

        if (organizeButton != null) {
            foreach (ClickableComponent item in ItemsToGrabMenu.GetBorder(InventoryMenu.BorderSide.Right)) {
                item.rightNeighborID = organizeButton.myID;
            }
        }

        if (trashCan != null && base.inventory.inventory.Count >= 12 && base.inventory.inventory[11] != null) {
            base.inventory.inventory[11].rightNeighborID = 5948;
        }

        if (trashCan != null) {
            trashCan.leftNeighborID = 11;
        }

        if (okButton != null) {
            okButton.leftNeighborID = 11;
        }

        ClickableComponent? top_right = ItemsToGrabMenu.GetBorder(InventoryMenu.BorderSide.Right).FirstOrDefault();

        if (top_right != null) {
            if (organizeButton != null) {
                organizeButton.leftNeighborID = top_right.myID;
            }

            if (specialButton != null) {
                specialButton.leftNeighborID = top_right.myID;
            }

            if (fillStacksButton != null) {
                fillStacksButton.leftNeighborID = top_right.myID;
            }

            if (junimoNoteIcon != null) {
                junimoNoteIcon.leftNeighborID = top_right.myID;
            }
        }

        populateClickableComponentList();

        if (Game1.options.SnappyMenus) {
            snapToDefaultClickableComponent();
        }

        SetupBorderNeighbors();
    }

    public CollectorItemGrabMenu(ItemGrabMenu menu)
        : this(menu.ItemsToGrabMenu.actualInventory, menu.reverseGrab, menu.showReceivingMenu, menu.inventory.highlightMethod, menu.behaviorFunction, menu.message, menu.behaviorOnItemGrab, snapToBottom: false, menu.canExitOnKey, menu.playRightClickSound, menu.allowRightClick, menu.organizeButton != null, menu.source, menu.sourceItem, menu.whichSpecialButton, menu.context, menu.HeldItemExitBehavior, menu.AllowExitWithHeldItem) {
        this.setEssential(menu.essential);
        if (menu.currentlySnappedComponent != null) {
            this.setCurrentlySnappedComponentTo(menu.currentlySnappedComponent.myID);
            if (Game1.options.SnappyMenus) {
                this.snapCursorToCurrentSnappedComponent();
            }
        }
        base.heldItem = menu.heldItem;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true) {
        base.receiveLeftClick(x, y, !this.destroyItemOnClick);
        if (this.organizeButton != null && this.organizeButton.containsPoint(x, y)) {
            ItemGrabMenu.organizeItemsInList(this.ItemsToGrabMenu.actualInventory);
            Game1.activeClickableMenu = new CollectorItemGrabMenu(this);
            Game1.playSound("Ship");
        }
    }

}