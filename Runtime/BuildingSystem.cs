// using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using m4k.Items;

namespace m4k.BuildSystem {

[System.Serializable]
public class BuildingSystemData {
    public SerializableDictionary<string, List<BuildingSystem.BuiltItem>> sceneBuiltItems;
}

[System.Serializable]
public class BuildingSystem : Singleton<BuildingSystem>
{
    [System.Serializable]
    public class BuiltItem {
        public string name;
        public Vector3 pos;
        public Quaternion rot;
        public int parentId;
        public string guid;
        
        [System.NonSerialized]
        public Item item;
        [System.NonSerialized]
        public GameObject instance;
        [System.NonSerialized]
        public BuildingSystemObject objComponent;
        [System.NonSerialized]
        public List<BuiltItem> children = new List<BuiltItem>();
    }
    public BuildingSystemUI UI;
    public AudioClip placeAudioClip;
    public Material invalidPlacementMat;
    public Material validPlacementMat;
    public Material highlightMat;
    public GameObject buildSystemUIParent;
    public Inventory buildableInventory;
    public List<ItemTag> ignoreQuantityTags;

    public float rotSpeed = 1;
    public float snapMov = 0.5f;
    public int snapRot = 45;
    public int buildingLayer, builtLayer;
    [Header("Layers that detect placement raycasts")]
    public LayerMask buildModeLayers;
    public Item currItem;

    public bool isBuilding { get; set; }
    public bool isSnapping { get; set; }
    public bool shiftMod { get; set; }

    GameObject currObjInstance;
    Item prevItem;
    BuiltItem currBuiltItem;
    BuildingSystemObject currBuildComponent;
    Transform currObjTransform, prevObjTransform;
    
    string currSceneName;
    float currFloorHeight = 0f;

    List<BuiltItem> currBuiltItems;
    SerializableDictionary<string, List<BuiltItem>> sceneBuiltItems = new SerializableDictionary<string, List<BuiltItem>>();

    // managed collection of ibuildables on builtItems; for aux event processing
    Dictionary<BuiltItem, IBuildable[]> builtBuildables = new Dictionary<BuiltItem, IBuildable[]>();
    // fetch list of built items by itemtag
    Dictionary<ItemTag, List<BuiltItem>> builtItemLists = new Dictionary<ItemTag, List<BuiltItem>>();

    private void Start() {
        if(!UI)
            UI = GetComponentInChildren<BuildingSystemUI>();

        // TODO: better inventory management for expandability
        buildableInventory = InventoryManager.I.GetOrRegisterSavedInventory("buildable", 100);
        buildableInventory.keepZeroItems = true;
        var items = AssetRegistry.I.GetItemListByType(typeof(ItemBuildable));
        foreach(var i in items)
            buildableInventory.AddItemAmount(i, 1);

        UI.Init(this);

        SceneHandler.I.onSceneChanged -= OnSceneChanged;
        SceneHandler.I.onSceneChanged += OnSceneChanged;
    }

    public Vector3 GetRandomPositionWithinZoneBounds() {
        Vector3 pos = Vector3.zero;

        var zones = GetOrAddBuiltItemsListByTag(ItemTag.Zone);
        float xmin = Mathf.Infinity, xmax = -Mathf.Infinity;
        float zmin = Mathf.Infinity, zmax = -Mathf.Infinity;
        for(int i = 0; i < zones.Count; ++i) {
            var p = zones[i].pos;
            if(p.x < xmin) xmin = p.x;
            if(p.x > xmax) xmax = p.x;
            if(p.z < zmin) zmin = p.x;
            if(p.z > zmax) zmax = p.x;
        }
        pos.Set(Random.Range(xmin, xmax), 0, Random.Range(zmin, zmax));

        return pos;
    }

    void OnSceneChanged() {
        if(SceneHandler.I.isMainMenu) {
            return;
        }
        // if current scene and current scene is equivalent to new scene, return
        if(!string.IsNullOrEmpty(currSceneName) 
        && SceneHandler.I.activeScene.name == currSceneName)
            return;

        // if active scene has built items, spawn and initiate
        if(sceneBuiltItems.TryGetValue(SceneHandler.I.activeScene.name, out currBuiltItems))
            SpawnSceneObjInstances();
    }

    /// <summary>
    /// Update or create built items list for current active scene. Only create entry in scenes that allow building; call when intializing new builtItem
    /// </summary>
    void UpdateCurrentSceneBuiltItems() {
        currSceneName = SceneHandler.I.activeScene.name;
        if(!sceneBuiltItems.TryGetValue(currSceneName, out currBuiltItems)) {
            currBuiltItems = new List<BuiltItem>();
            sceneBuiltItems.Add(currSceneName, currBuiltItems);
        }
    }

    public List<BuiltItem> GetOrAddBuiltItemsListByTag(ItemTag tag) {
        List<BuiltItem> builtItemsList;
        builtItemLists.TryGetValue(tag, out builtItemsList);
        if(builtItemsList == null) {
            builtItemsList = new List<BuiltItem>();
            builtItemLists.Add(tag, builtItemsList);
        }
        return builtItemsList;
    }

    // Buildable inventory management

    public void AddBuildItem(Item item, int amount) {
        foreach(var tag in ignoreQuantityTags)
            if(item.HasTag(tag)) return;
        buildableInventory.AddItemAmount(item, amount);
    }

    public void RemoveBuildItem(Item item, int amount) {
        foreach(var tag in ignoreQuantityTags)
            if(item.HasTag(tag)) return;
        buildableInventory.RemoveItemAmount(item, amount, true);
    }


    /// <summary>
    /// Used by UI to assign buildable item inventory based on predicate.
    /// </summary>
    /// <param name="filter">Predicate to filter items</param>
    public void ToggleBuildTab(System.Predicate<ItemInstance> filter) {
        UI.buildItemSlotManager.AssignInventory(buildableInventory, filter);
    }

    /// <summary>
    /// Main build mode toggle
    /// </summary>
    public void ToggleBuildMode() {
        CancelInput();
        UI.ToggleBuildMode(!buildSystemUIParent.activeInHierarchy);
        isBuilding = buildSystemUIParent.activeInHierarchy;
        ToggleAllBuildableVisuals(isBuilding);
    }

    /// <summary>
    /// If actively editing/placing buildable object, destroy it and its children
    /// </summary>
    public void Cleanup() {
        if(currObjInstance)
            DestroyBuiltObjRecurs(currBuiltItem);
    }

    // Input methods

    public void HitInput(RaycastHit hit) {
        if(!currObjInstance) return;

        Vector3 pos = hit.point + Vector3.up * 0.01f;
        if(currItem && currItem.HasTag(ItemTag.Floor)) 
            pos.y = currFloorHeight;
        
        if(isSnapping) {
            pos.x = Mathf.Round(pos.x / snapMov) * snapMov;
            pos.z = Mathf.Round(pos.z / snapMov) * snapMov;
        }
        currObjTransform.position = pos;
    }

    public void ConfirmInput(RaycastHit hit) {
        if(currObjInstance) {
            if(!buildObjColliding) {
                UI.canvasGroup.FadeIn();
                PlaceBuildObject(hit.transform.parent);
                if(shiftMod)
                    SetBuildObject(prevItem);
            }
            else {
                Feedback.I.SendLine("Invalid build location, colliding");
            }
        }
        else if(hit.transform.gameObject.layer == builtLayer || 
                hit.transform.CompareTag("Buildable")) 
        {
            EditBuildObject(hit.transform.parent.gameObject);
        }
    }

    public void CancelInput() {
        if(!currObjInstance) return;

        UI.canvasGroup.FadeIn();
        DestroyBuiltObjRecurs(currBuiltItem);
    }

    public void RotateLeft() {
        if(!currObjInstance) return;

        var rot = isSnapping ? snapRot : rotSpeed;
        currObjTransform.Rotate(0, -rot, 0);
    }

    public void RotateRight() {
        if(!currObjInstance) return;

        var rot = isSnapping ? snapRot : rotSpeed;
        currObjTransform.Rotate(0, rot, 0);
    }

    /// <summary>
    /// Start editing/placing item
    /// </summary>
    /// <param name="item"></param>
    public void SetBuildObject(Item item, bool forceBuildMode = false) {
        if(currObjInstance) {
            return;
        }
        if(forceBuildMode && !isBuilding) {
            ToggleBuildMode();
        }
        // Debug.Log($"Set buildable {item.name}");
        currItem = item;
        var rot = shiftMod && prevObjTransform ? prevObjTransform.rotation : item.prefab.transform.rotation;
        GameObject obj = Instantiate(item.prefab, Vector3.down * 50f, rot);

        EditBuildObject(obj);
    }

    /// <summary>
    /// Begin placement/editing of a buildable object. Confirm BuildingSystemObject component and IBuildable CanEdit if existing. Called both when building new object from inventory and attempting to edit existing built object.
    /// </summary>
    /// <param name="instance">New prefab instance or existing built object</param>
    public void EditBuildObject(GameObject instance) {
        if(currBuiltItem != null)
            currBuildComponent = currBuiltItem.objComponent;
        if(!currBuildComponent)
            currBuildComponent = instance.GetComponentInChildren<BuildingSystemObject>();
        if(!currBuildComponent)
            Debug.LogWarning("No BuildingSystemObject on current buildable");

        if(currBuildComponent.builtItem != null && builtBuildables.TryGetValue(currBuildComponent.builtItem, out var buildables)) {
            foreach(var buildable in buildables)
                if(!buildable.CanEdit()) {
                    Feedback.I.SendLine($"Cannot edit {currBuildComponent.builtItem.item.displayName}");
                    return;
                }
        }

        UI.canvasGroup.FadeOut();
        currObjInstance = instance;
        currObjTransform = instance.transform;

        if(!currItem)
            currItem = currBuildComponent.item;

        GetOrCreateBuiltItem(instance);
        InitBuildObjRecurs(currBuiltItem);
    }

    /// <summary>
    /// Called if actively placing/editing object and confirm input received. Finalizes build object placement, updates stored date, and cleans up stale references.
    /// </summary>
    /// <param name="newParent"></param>
    void PlaceBuildObject(Transform newParent) {
        RemoveBuildItem(currBuiltItem.item, 1);
        Feedback.I.PlayAudio(placeAudioClip);
        FinalizeBuildObjRecurs(currBuiltItem);
        GetOrCreateBuiltItem(currObjInstance);
        FinalizePlacement(newParent);
        CleanBuildable();
    }

    /// <summary>
    /// Get or create built item from instance. If created, initialize and add built item to collection.
    /// </summary>
    void GetOrCreateBuiltItem(GameObject instance) {
        UpdateCurrentSceneBuiltItems();
        int instanceId = FindBuiltItemIndexFromInstance(instance);

        if(instanceId == -1) {
            currBuiltItem = new BuiltItem(); 
            currBuiltItem.item = currItem;
            currBuiltItem.name = currItem.name;
            currBuiltItem.instance = instance;
            currBuiltItem.parentId = -1;
            
            currBuiltItem.objComponent = currBuildComponent;
            currBuiltItem.objComponent.Initialize(this, currBuiltItem);

            if(currBuiltItem.objComponent.guidComponent)
                currBuiltItem.guid = currBuiltItem.objComponent.guidComponent.GetGuid().ToString();

            UpdateBuiltBuildables(currBuiltItem);
            
            currBuiltItems.Add(currBuiltItem);
            foreach(var tag in currBuiltItem.item.itemTags)
                GetOrAddBuiltItemsListByTag(tag).Add(currBuiltItem);
        }
        else {
            currBuiltItem = currBuiltItems[instanceId];
            currItem = currBuiltItem.item;
            currBuildComponent = currBuiltItem.objComponent;
        }
    }

    /// <summary>
    /// Finalize parent-child relations; update active and stored transform information
    /// </summary>
    /// <param name="newParent"></param>
    void FinalizePlacement(Transform newParent) {
        if(currBuiltItem.parentId != -1) {
            currBuiltItems[currBuiltItem.parentId].children.Remove(currBuiltItem);
            currBuiltItem.parentId = -1;
        }

        if(newParent) {
            int parentInd = FindBuiltItemIndexFromInstance(newParent.gameObject);
            if(parentInd != -1) {
                currBuiltItems[parentInd].children.Add(currBuiltItem);
                currObjInstance.transform.SetParent(newParent, true);
            }
            else {
                currObjInstance.transform.SetParent(null);
            }
            currBuiltItem.parentId = parentInd;
        } 
        else {
            currObjInstance.transform.SetParent(null);
        }

        UpdateBuiltPosRotRecurs(currBuiltItem);

        Debug.Log($"Built {currBuiltItem.item.displayName}; Num built items: {currBuiltItems.Count}");
    }


    /// <summary>
    /// Build cache of IBuildables for built items. Called on built item initialization
    /// </summary>
    /// <param name="builtItem"></param>
    void UpdateBuiltBuildables(BuiltItem builtItem) {
        if(builtBuildables.ContainsKey(builtItem))
            return;
        var buildableComps = builtItem.instance.GetComponentsInChildren<IBuildable>();
        if(buildableComps != null && buildableComps.Length > 0) {
            builtBuildables[builtItem] = buildableComps;
        }
    }

    // Recursive methods for updating state of built item and its children

    void InitBuildObjRecurs(BuiltItem builtItem) {
        for(int i = 0; i < builtItem.children.Count; ++i) {
            InitBuildObjRecurs(builtItem.children[i]);
        }
        builtItem.objComponent.StartPlacement();
        ToggleBuildableEditing(builtItem, true);
    }

    void FinalizeBuildObjRecurs(BuiltItem builtItem) {
        for(int i = 0; i < builtItem.children.Count; ++i) {
            FinalizeBuildObjRecurs(builtItem.children[i]);
        }
        ToggleBuildableEditing(builtItem, false);
        builtItem.objComponent.FinalizePlacement();
    }

    void UpdateBuiltPosRotRecurs(BuiltItem builtItem) {
        for(int i = 0; i < builtItem.children.Count; ++i) {
            UpdateBuiltPosRotRecurs(builtItem.children[i]);
        }
        builtItem.pos = builtItem.instance.transform.position;
        builtItem.rot = builtItem.instance.transform.rotation;
    }

    void DestroyBuiltObjRecurs(BuiltItem builtItem) {
        AddBuildItem(builtItem.item, 1);
        for(int i = 0; i < builtItem.children.Count; ++i) {
            DestroyBuiltObjRecurs(builtItem.children[i]);
        }
        Destroy(builtItem.instance);
        currBuiltItems.Remove(builtItem);
        foreach(var tag in currBuiltItem.item.itemTags)
            GetOrAddBuiltItemsListByTag(tag).Remove(currBuiltItem);
        
        if(builtBuildables.ContainsKey(builtItem))
            builtBuildables.Remove(builtItem);

        if(builtItem == currBuiltItem)
            CleanBuildable();
    }


    public int FindBuiltItemIndexFromInstance(GameObject instance) {
        return currBuiltItems.FindIndex(x=>x.instance == instance);
    }

    void CleanBuildable() {
        currObjInstance = null;
        prevItem = currItem;
        currItem = null;
        currBuildComponent = null;
        prevObjTransform = currObjTransform;
        currObjTransform = null;
        currBuiltItem = null;
        buildObjColliding = false;
    }

    bool buildObjColliding;
    /// <summary>
    /// Called by trigger events on buildingSystemObject components to set colliding flag
    /// </summary>
    /// <param name="colliding"></param>
    /// <param name="snappable"></param>
    /// <param name="other"></param>
    public void SetBuildObjectColliding(bool colliding, bool snappable, Collider other) {
        if(buildObjColliding == colliding) 
            return;

        buildObjColliding = colliding;
    }
    
    /// <summary>
    /// Toggle all building system visuals. Toggle with build mode active
    /// </summary>
    /// <param name="b"></param>
    public void ToggleAllBuildableVisuals(bool b) {
        foreach(var i in builtBuildables.Values) {
            foreach(var j in i) {
                j.OnToggleBuildableVisual(b);
            }
        }
    }

    /// <summary>
    /// Toggle with active object edit
    /// </summary>
    /// <param name="builtItem"></param>
    /// <param name="enabled"></param>
    void ToggleBuildableEditing(BuiltItem builtItem, bool enabled) {
        if(builtBuildables.TryGetValue(builtItem, out var t))
            foreach(var i in t) {
                if(enabled) {
                    i.OnToggleBuildableVisual(enabled);
                }
                i.OnToggleBuildableEdit(enabled);
            }
    }
    
    /// <summary>
    /// Spawn and initialize saved built items in current active scene. Called on scene change
    /// </summary>
    void SpawnSceneObjInstances() {
        for(int i = 0; i < currBuiltItems.Count; ++i) {
            BuiltItem builtItem = currBuiltItems[i];
            if(!currBuiltItems[i].item) {
                currBuiltItems[i].item = AssetRegistry.I.GetItemFromName(currBuiltItems[i].name);
            }

            if(!builtItem.instance) {
                builtItem.instance = Instantiate(builtItem.item.prefab);

                builtItem.instance.transform.position = builtItem.pos;
                builtItem.instance.transform.rotation = builtItem.rot;
                builtItem.objComponent = builtItem.instance.GetComponentInChildren<BuildingSystemObject>();
                builtItem.objComponent.Initialize(this, builtItem);

                if(!string.IsNullOrEmpty(builtItem.guid))
                    builtItem.objComponent.guidComponent.SetGuid(builtItem.guid);

                UpdateBuiltBuildables(builtItem);
            }

            currBuiltItems[i] = builtItem;
        }

        for(int i = 0; i < currBuiltItems.Count; ++i) {
            if(currBuiltItems[i].parentId != -1) {
                var parent = currBuiltItems[currBuiltItems[i].parentId];
                currBuiltItems[i].instance.transform.SetParent(parent.instance.transform, true);
                parent.children.Add(currBuiltItems[i]);
            }
        }
        ToggleAllBuildableVisuals(false);
    }

    public void Serialize(ref BuildingSystemData data) {
        data.sceneBuiltItems = this.sceneBuiltItems;
    }
    public void Deserialize(ref BuildingSystemData data) {
        this.sceneBuiltItems = data.sceneBuiltItems;
    }
}

public interface IBuildable {
    /// <summary>
    /// Enabled when any buildable is in edit/placement mode
    /// </summary>
    /// <param name="b"></param>
    void OnToggleBuildableEdit(bool b);

    /// <summary>
    /// Visuals are enabled when build mode is active
    /// </summary>
    /// <param name="b"></param>
    void OnToggleBuildableVisual(bool b);

    /// <summary>
    /// On built item selected, determine if can be editted
    /// </summary>
    /// <returns></returns>
    bool CanEdit();
}
}