// using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using m4k.InventorySystem;
using m4k.Characters;
// public class BuildType {
//     // string name;
//     // int value;
//     // public static readonly BuildType Light = new BuildType("Light", 0);
//     public static readonly string Light = "Light";
//     public static readonly string Floor = "Floor";
//     public static readonly string Table = "Table";
//     public static readonly string Seat = "Seat";
//     public static readonly string Prop = "Prop";
//     public static readonly string Zone = "Zone";
//     public static readonly string Wall = "Wall";
//     // public BuildType(string n, int v) {
//     //     name = n;
//     //     value = v;
//     // }
//     // public override string ToString() { return name; }
//     // public int GetValue() { return value; }

// }
namespace m4k.BuildSystem {

[System.Serializable]
public class BuildingSystemData {
    public List<BuildingSystem.BuiltItem> builtItems;
}

[System.Serializable]
public class BuildingSystem : Singleton<BuildingSystem>//, IStateSerializable
{
    [System.Serializable]
    public class BuiltItem {
        public string name;
        public Vector3 pos;
        public Quaternion rot;
        public int sceneId;
        public int parentId;
        
        [System.NonSerialized]
        public Item item;
        [System.NonSerialized]
        public GameObject instance;
        [System.NonSerialized]
        public BuildingSystemObject objComponent;
        [System.NonSerialized]
        public Renderer renderer;
        [System.NonSerialized]
        public List<BuiltItem> children = new List<BuiltItem>();
        // public ItemTag tag { get { return item.itemTags[0]; }}
    }
    public BuildingSystemUI UI;
    public AudioClip placeAudioClip;
    public List<BuiltItem> builtItems;
    public Material invalidPlacementMat;
    public Material validPlacementMat;
    public Material highlightMat;
    public GameObject buildSystemUIParent;
    public Inventory buildableInventory;
    public bool isSnapping;
    public float rotSpeed = 1;
    public float snapMov = 0.5f;
    public int snapRot = 45;
    public int buildingLayer, builtLayer;
    public Item currItem;

    [System.NonSerialized]
    public bool isBuilding;
    public bool shiftMod;

    GameObject currObjInstance;
    Item prevItem;
    BuiltItem currBuiltItem;
    BuildingSystemObject currBuildComponent;
    Transform currObjTransform, prevObjTransform;
    bool isCollideSnap, prevRot;
    Vector3 prevPos;
    Collider otherCollider, currObjCol;
    Dictionary<BuiltItem, IBuildable[]> buildables;
    Dictionary<ItemTag, Inventory> tagInv = new Dictionary<ItemTag, Inventory>();
    Dictionary<ItemTag, List<BuiltItem>> builtItemLists = new Dictionary<ItemTag, List<BuiltItem>>();

    private void Start() {
        if(!UI)
            UI = GetComponentInChildren<BuildingSystemUI>();

        buildableInventory = InventoryManager.I.GetOrRegisterInventory("buildable", 100, 0, true);
        buildableInventory.condHide = true;
        var items = AssetRegistry.I.GetItemListByType(ItemType.Buildable);
        foreach(var i in items)
            buildableInventory.AddItemAmount(i, 1);

        UI.Init(this);
        builtItems = new List<BuiltItem>();
        buildables = new Dictionary<BuiltItem, IBuildable[]>();
        buildingLayer = LayerMask.NameToLayer("Immaterial");
        builtLayer = LayerMask.NameToLayer("Buildable");

        foreach(var i in UI.buildItemLists) {
            tagInv.Add(i.tag, i.inv);
        }

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

    public Vector3 GetRandomFloorPosition() {
        var floors = GetOrAddBuiltItemsListByTag(ItemTag.Floor);
        var randFloor = Random.Range(0, floors.Count);

        return floors[randFloor].pos;
    }
    public Vector3 GetRandomTable() {
        var tables = GetOrAddBuiltItemsListByTag(ItemTag.Table);
        var randTable = Random.Range(0, tables.Count);

        return tables[randTable].pos;
    }

    void OnSceneChanged() {
        if(SceneHandler.I.isMainMenu) {
            builtItems.Clear();
            buildables.Clear();
            return;
        }
        SpawnSceneObjInstances();
    }

    public List<BuiltItem> GetOrAddBuiltItemsListByTag(ItemTag tag) {
        List<BuiltItem> builtItems;
        builtItemLists.TryGetValue(tag, out builtItems);
        if(builtItems == null) {
            builtItems = new List<BuiltItem>();
            builtItemLists.Add(tag, builtItems);
        }
        return builtItems;
    }
    Inventory GetInventory(ItemTag tag) {
        Inventory inv;
        tagInv.TryGetValue(tag, out inv);
        if(inv == null) 
            Debug.LogWarning("Build inv not found");
        return inv;
    }
    public void AddBuildItem(Item item, int amount) {
        if(item.itemTags[0] == ItemTag.Zone) return;
        // Inventory inv = GetInventory(item.itemTags[0]);
        buildableInventory.AddItemAmount(item, amount);
    }
    public void RemoveBuildItem(Item item, int amount) {
        if(item.itemTags[0] == ItemTag.Zone) return;
        // Inventory inv = GetInventory(item.itemTags[0]);
        buildableInventory.RemoveItemAmount(item, amount, true);
    }
    public void ToggleBuildTab(System.Predicate<ItemInstance> filter) {
        UI.buildItemSlotManager.AssignInventory(buildableInventory, filter);
    }
    public void ToggleBuildMode() {
        CancelInput();
        UI.ToggleBuildMode(!buildSystemUIParent.activeInHierarchy);
        isBuilding = buildSystemUIParent.activeInHierarchy;
        SetToggleBuildableVisuals(isBuilding);
    }
    public void Cleanup() {
        if(currObjInstance)
            DestroyBuiltObjRecurs(currBuiltItem);
    }

    public void HitInput(RaycastHit hit) {
        if(!currObjInstance) return;

        Vector3 pos = hit.point + Vector3.up * 0.01f;
        if(currItem && currItem.HasTag(ItemTag.Floor)) 
            pos.y = 0;
        
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

        if(!currItem.HasTag(ItemTag.Zone)) {
            var rot = isSnapping ? snapRot : rotSpeed;
            currObjTransform.Rotate(0, -rot, 0);
        }
    }
    public void RotateRight() {
        if(!currObjInstance) return;

        if(!currItem.HasTag(ItemTag.Zone)) {
            var rot = isSnapping ? snapRot : rotSpeed;
            currObjTransform.Rotate(0, rot, 0);
        }
    }

    public void SetBuildObject(Item item) {
        if(currObjInstance) {
            return;
        }
        Debug.Log($"Set buildable {item.name}");
        currItem = item;
        var rot = shiftMod && prevObjTransform ? prevObjTransform.rotation : item.prefab.transform.rotation;
        GameObject obj = Instantiate(item.prefab, Vector3.up * 50, rot);

        EditBuildObject(obj);
    }

    public void EditBuildObject(GameObject instance) {
        if(currBuiltItem != null)
            currBuildComponent = currBuiltItem.objComponent;
        if(!currBuildComponent)
            // currBuildComponent = instance.GetComponentInChildren<Collider>().gameObject.GetComponentInChildren<BuildingSystemObject>();
            currBuildComponent = instance.GetComponentInChildren<BuildingSystemObject>();
        // if(!currBuildComponent || !currBuildComponent.item) {
        //     CleanBuildable();
        //     return;
        // }

        UI.canvasGroup.FadeOut();
        currObjInstance = instance;
        currObjTransform = instance.transform;
        currObjCol = instance.GetComponentInChildren<Collider>();
        prevPos = instance.transform.position;

        if(!currItem)
            currItem = currBuildComponent.item;

        UpdateBuiltItems(null);
        InitBuildObjRecurs(currBuiltItem);
    }

    void PlaceBuildObject(Transform newParent) {
        RemoveBuildItem(currBuiltItem.item, 1);
        Feedback.I.PlayAudio(placeAudioClip);
        FinalizeBuildObjRecurs(currBuiltItem);
        UpdateBuiltItems(newParent);
        CleanBuildable();
    }

    void UpdateBuiltItems(Transform newParent) {
        int instanceId = FindBuiltItemIndexFromInstance(currObjInstance);

        if(instanceId == -1) {
            currBuiltItem = new BuiltItem(); 
            currBuiltItem.item = currItem;
            currBuiltItem.name = currItem.itemName;
            currBuiltItem.instance = currObjInstance;
            currBuiltItem.objComponent = currBuildComponent;
            currBuiltItem.renderer = currObjInstance.GetComponentInChildren<Renderer>();
            currBuiltItem.sceneId = SceneHandler.I.activeScene.buildIndex;
            
            builtItems.Add(currBuiltItem);
            GetOrAddBuiltItemsListByTag(currBuiltItem.item.itemTags[0]).Add(currBuiltItem);
            Debug.Log("Num built items: " + builtItems.Count);
        }
        else {
            currBuiltItem = builtItems[instanceId];
            currItem = currBuiltItem.item;
            currBuildComponent = currBuiltItem.objComponent;
        }

        if(currBuiltItem.parentId != -1) {
            builtItems[currBuiltItem.parentId].children.Remove(currBuiltItem);
            currBuiltItem.parentId = -1;
        }

        if(newParent) {
            int parentInd = FindBuiltItemIndexFromInstance(newParent.gameObject);
            if(parentInd != -1) {
                builtItems[parentInd].children.Add(currBuiltItem);
                currObjInstance.transform.SetParent(newParent, true);
            }
            else {
                currObjInstance.transform.SetParent(null);
            }
            builtItems[instanceId].parentId = parentInd;
        }

        UpdateBuiltPosRotRecurs(currBuiltItem);
    }

    void UpdateItemTypes(BuiltItem builtItem, bool finalizing) {
        var buildableComps = builtItem.instance.GetComponentsInChildren<IBuildable>();
        if(buildableComps != null && buildableComps.Length > 0) {
            if(!buildables.ContainsKey(builtItem))
                buildables[builtItem] = buildableComps;
        }
        ToggleBuildableEditing(builtItem, !finalizing);
    }

    void ToggleBuildableEditing(BuiltItem builtItem, bool b) {
        IBuildable[] t;
        buildables.TryGetValue(builtItem, out t);
        if(t != null) {
            foreach(var i in t) {
                if(b) {
                    i.OnToggleBuildableVisual(b);
                }
                i.OnToggleBuildableEdit(b);
            }
        }
    }

    void InitBuildObjRecurs(BuiltItem builtItem) {
        for(int i = 0; i < builtItem.children.Count; ++i) {
            InitBuildObjRecurs(builtItem.children[i]);
        }
        builtItem.objComponent.Initialize(this);
        UpdateItemTypes(builtItem, false);
    }
    void FinalizeBuildObjRecurs(BuiltItem builtItem) {
        for(int i = 0; i < builtItem.children.Count; ++i) {
            FinalizeBuildObjRecurs(builtItem.children[i]);
        }
        UpdateItemTypes(builtItem, true);
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
        builtItems.Remove(builtItem);
        GetOrAddBuiltItemsListByTag(currBuiltItem.item.itemTags[0]).Remove(currBuiltItem);
        
        if(buildables.ContainsKey(builtItem))
            buildables.Remove(builtItem);

        if(builtItem == currBuiltItem)
            CleanBuildable();
    }

    public int FindBuiltItemIndexFromInstance(GameObject instance) {
        return builtItems.FindIndex(x=>x.instance == instance);
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
    public void SetBuildObjectColliding(bool colliding, bool snappable, Collider other) {
        if(buildObjColliding == colliding) 
            return;

        buildObjColliding = colliding;
        isCollideSnap = snappable;
        otherCollider = other;
    }
    public void SetToggleBuildableVisuals(bool b) {
        foreach(var i in buildables.Values) {
            foreach(var j in i) {
                j.OnToggleBuildableVisual(b);
            }
        }
    }
    
    public void SpawnSceneObjInstances() {
        Debug.Log("Build scene change, items: " + builtItems.Count);
        for(int i = 0; i < builtItems.Count; ++i) {
            BuiltItem builtItem = builtItems[i];
            if(!builtItems[i].item) {
                builtItems[i].item = AssetRegistry.I.GetItemFromName(builtItems[i].name);
            }
            if(builtItem.sceneId != SceneHandler.I.activeScene.buildIndex) {
                Debug.Log(builtItem.item.itemName + " not in this scene");
            }

            if(builtItem.sceneId == SceneHandler.I.activeScene.buildIndex && !builtItem.instance) {
                builtItem.instance = Instantiate(builtItem.item.prefab);

                builtItem.instance.transform.position = builtItem.pos;
                builtItem.instance.transform.rotation = builtItem.rot;
                builtItem.objComponent = builtItem.instance.GetComponentInChildren<BuildingSystemObject>();
                builtItem.renderer = builtItem.instance.GetComponentInChildren<Renderer>();

                UpdateItemTypes(builtItem, true);
            }
            builtItems[i] = builtItem;
        }
        for(int i = 0; i < builtItems.Count; ++i) {
            if(builtItems[i].sceneId == SceneHandler.I.activeScene.buildIndex && builtItems[i].parentId != -1) {
                builtItems[i].instance.transform.SetParent(builtItems[builtItems[i].parentId].instance.transform, true);
                builtItems[builtItems[i].parentId].children.Add(builtItems[i]);
            }
        }
        SetToggleBuildableVisuals(false);
    }
    // public void Serialize(ref GameDataWriter writer) {
    //     writer.Write(builtItems.Count);
        
    //     for(int i = 0; i < builtItems.Count; ++i) {
    //         writer.Write(builtItems[i].item.guid);
    //         writer.Write(builtItems[i].instance.transform.position);
    //         writer.Write(builtItems[i].instance.transform.rotation);
    //         writer.Write(builtItems[i].sceneId);
    //         writer.Write(builtItems[i].parentId);
    //     }
    // }

    // public void Deserialize(ref GameDataReader reader) {
    //     int len = reader.ReadInt();
    //     Debug.Log("Build items deserialized: " + len);

    //     for(int i = 0; i < len; ++i) {
    //         string guid = reader.ReadString();
    //         Item itemN = AssetRegistry.I.GetItemFromGuid(guid);

    //         BuiltItem builtItem = new BuiltItem() {
    //             item = itemN,
    //             pos = reader.ReadVector3(),
    //             rot = reader.ReadQuaternion(),
    //             sceneId = reader.ReadInt(),
    //             parentId = reader.ReadInt(),
    //         };

    //         builtItems.Add(builtItem);
    //     }
    // }
}

public interface IBuildable {
    void OnToggleBuildableEdit(bool b);
    void OnToggleBuildableVisual(bool b);
}
}