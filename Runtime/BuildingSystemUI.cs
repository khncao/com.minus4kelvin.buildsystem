// using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using m4k.Items;

namespace m4k.BuildSystem {
public class BuildingSystemUI : MonoBehaviour
{
    [System.Serializable]
    public class BuildItemList {
        public Toggle toggle;
        public ItemTag tag;
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
        EventSystem.current.SetSelectedGameObject(buildItemSlotManager.slots[0].gameObject);
    }

    public void OnToggle(Toggle changed) {
        int listInd = buildItemLists.FindIndex(x=>x.toggle == changed);
        if(listInd == -1) {
            Debug.LogError("list not found");
            return;
        }

        buildingSystem.ToggleBuildTab(x=>x.item.HasTag(buildItemLists[listInd].tag));
        
        scroll.normalizedPosition = Vector2.zero;
        EventSystem.current.SetSelectedGameObject(buildItemSlotManager.slots[0].gameObject);
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