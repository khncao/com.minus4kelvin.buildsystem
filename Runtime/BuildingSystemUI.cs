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
    public Toggle collapseToggle;
    public RectTransform[] collapsibleRTs;
    public float yExpanded, yCollapsed;
    public bool collapsed;
    public ScrollRect scroll;
    public Toggle snapToggle;
    public List<BuildItemList> buildItemLists;
    public ItemSlotHandler buildItemSlotManager;

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

        snapToggle.isOn = buildingSystem.isSnapping;
        snapToggle.onValueChanged.AddListener(OnSnapToggle);
        collapseToggle.onValueChanged.AddListener(OnToggleCollapse);
        snapToggle.isOn = collapsed;
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
        if(buildItemLists[listInd].tag == ItemTag.Consumable)
            buildingSystem.ToggleBuildTab(x=>!x.item.HasTag(ItemTag.Zone));
        else
            buildingSystem.ToggleBuildTab(x=>x.item.HasTag(buildItemLists[listInd].tag));
        
        scroll.normalizedPosition = Vector2.zero;
    }   

    void OnButtonClick(Item item) {
        buildingSystem.SetBuildObject(item);
    }

    void OnSnapToggle(bool on) {
        buildingSystem.isSnapping = on;
    }

    void OnToggleCollapse(bool b) {
        collapsed = b;
        for(int i = 0; i < collapsibleRTs.Length; ++i) {
            if(collapsed) {
                collapsibleRTs[i].sizeDelta = new Vector2(collapsibleRTs[i].rect.width, yCollapsed);
            }
            else {
                collapsibleRTs[i].sizeDelta = new Vector2(collapsibleRTs[i].rect.width, yExpanded);
            }
        }
    }
}
}