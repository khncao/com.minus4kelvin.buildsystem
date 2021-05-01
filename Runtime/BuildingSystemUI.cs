// using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using m4k.InventorySystem;

namespace m4k.BuildSystem {
public class BuildingSystemUI : MonoBehaviour
{
    [System.Serializable]
    public class BuildItemList {
        public Toggle toggle;
        public ItemTag tag;
        [HideInInspector]
        public Inventory inv;
    }
    public CanvasGroup canvasGroup;
    public GameObject objectButtonPrefab;
    public ScrollRect scroll;
    public GameObject serveButtonsParent, waitButtonsParent;
    public Button toggleGroupZonesButton;
    public Button createServeZoneButton1, createServeZoneButton2, createWaitZoneButton1, createWaitZoneButton2;
    public Toggle snapToggle;
    public Item[] serveZoneItems, waitZoneItems;
    public List<BuildItemList> buildItemLists;
    public ItemSlotHandler buildItemSlotManager;

    TMPro.TMP_Text toggleZoneText;
    BuildingSystem buildingSystem;

    private void OnDisable() {
        buildingSystem.Cleanup();
    }

    public void Init(BuildingSystem bs)
    {
        buildingSystem = bs;

        for(int i = 0; i < buildItemLists.Count; ++i) {
            // buildItemLists[i].inv = Game.Inventory.GetOrRegisterInventory("build" + buildItemLists[i].tag.ToString(), 100, 0, true);
            
            // if inventory for category not initialized, initialize
            // TODO: change from complete static db list to base+progressive
            // static list, lock at 0 quant; toggle hide locked

            // if(buildItemLists[i].inv != null && buildItemLists[i].inv.totalItemsList.Count == 0) {
            //     var list = Game.AssetRegistry.GetItemListByTag(buildItemLists[i].tag);
            //     if(list == null) {
            //         Debug.Log($"No {buildItemLists[i].tag} found");
            //         continue;
            //     }
            //     for(int j = 0; j < list.Count; ++j) {
            //         buildItemLists[i].inv.AddItemAmount(list[j], 1, false);
            //     }
            // }

            var tog = buildItemLists[i].toggle;
            tog.onValueChanged.AddListener(delegate { OnToggle(tog); });
        }
        OnToggle(buildItemLists[0].toggle);

        createServeZoneButton1.onClick.AddListener(()=>OnButtonClick(serveZoneItems[0]));
        createServeZoneButton2.onClick.AddListener(()=>OnButtonClick(serveZoneItems[1]));
        createWaitZoneButton1.onClick.AddListener(()=>OnButtonClick(waitZoneItems[0]));
        createWaitZoneButton2.onClick.AddListener(()=>OnButtonClick(waitZoneItems[1]));
        toggleGroupZonesButton.onClick.AddListener(()=>OnToggleZoneTypes());
        toggleZoneText = toggleGroupZonesButton.GetComponentInChildren<TMPro.TMP_Text>();
        OnToggleZoneTypes();
        snapToggle.isOn = buildingSystem.isSnapping;
        snapToggle.onValueChanged.AddListener(OnSnapToggle);
    }

    public void ToggleBuildMode(bool b) {
        gameObject.SetActive(b);
    }

    public void OnToggle(Toggle changed) {
        int listInd = buildItemLists.FindIndex(x=>x.toggle == changed);
        if(listInd == -1) {
            Debug.LogError("list not found");
            return;
        }
        // buildItemSlotManager.AssignInventory(buildItemLists[listInd].inv);
        // Game.Inventory.ToggleBuild(x=>x.item.HasTag(buildItemLists[listInd].tag));
        buildingSystem.ToggleBuildTab(x=>x.item.HasTag(buildItemLists[listInd].tag));
        
        scroll.normalizedPosition = Vector2.zero;
    }   

    void OnButtonClick(Item item) {
        buildingSystem.SetBuildObject(item);
    }
    int currType = 1;
    void OnToggleZoneTypes() {
        currType = (currType + 1) % 2;
        switch(currType) {
            case 0:
                toggleGroupZonesButton.targetGraphic.color = Color.yellow;
                toggleZoneText.text = "Serve";
                serveButtonsParent.SetActive(true);
                waitButtonsParent.SetActive(false);
                break;
            case 1:
                toggleGroupZonesButton.targetGraphic.color = Color.cyan;
                toggleZoneText.text = "Queue";
                serveButtonsParent.SetActive(false);
                waitButtonsParent.SetActive(true);
                break;
        }
    }

    void OnSnapToggle(bool on) {
        buildingSystem.isSnapping = on;
    }
}
}