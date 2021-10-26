using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using m4k.BuildSystem;
using m4k.InventorySystem;


namespace m4k {
#if UNITY_EDITOR
public class GenerateItemFromPrefab : MonoBehaviour
{
    static string buildablesPath = "Assets/Data/Buildables/";
    static string equipmentPath = "Assets/Data/Equipment/";
    // static string inventoryPath = "Assets/Data/Inventory/";
    
    [MenuItem("Tools/Create Items From Prefabs/Buildable")]
    // static void CreatePropItem() {
    //     CreateItem(ItemType.Prop);
    // }
    static void CreateBuildableItem() {
        var objs = Selection.gameObjects;
        for(int i = 0; i < objs.Length; ++i) {
            var newItem = ScriptableObject.CreateInstance<ItemBuildable>();
            newItem.itemName = objs[i].name;
            newItem.itemType = ItemType.Buildable;
            newItem.maxAmount = 999;
            // newItem.itemType = itemType;
            newItem.prefab = objs[i];
            // newItem.prefabRef = new UnityEngine.AddressableAssets.AssetReferenceT<GameObject>(GetGUID(objs[i])); 
            string assetPath = buildablesPath + objs[i].name + ".asset";
            AssetDatabase.CreateAsset(newItem, assetPath);
            AssetDatabase.SaveAssets();

            GameObject assetRoot = objs[i] as GameObject;
            string prefabPath = AssetDatabase.GetAssetPath(assetRoot);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            var col = prefabRoot.GetComponentInChildren<Collider>();
            if(!col) Debug.LogWarning("Collider not found");
            var builtComponent = col.GetComponent<BuildingSystemObject>();
            if(!builtComponent) builtComponent = col.gameObject.AddComponent<BuildingSystemObject>();
            builtComponent.item = AssetDatabase.LoadAssetAtPath<ItemBuildable>(assetPath);
            col.gameObject.layer = LayerMask.NameToLayer("Buildable");

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
    [MenuItem("Tools/Create Items From Prefabs/Equipment")]
    static void CreateEquipment() {
        var objs = Selection.gameObjects;
        for(int i = 0; i < objs.Length; ++i) {
            var newItem = ScriptableObject.CreateInstance<Item>();
            newItem.itemName = objs[i].name;
            newItem.itemType = ItemType.Equip;
            newItem.prefab = objs[i];
            // newItem.prefabRef = new UnityEngine.AddressableAssets.AssetReferenceT<GameObject>(GetGUID(objs[i]));
            string assetPath = equipmentPath + objs[i].name + ".asset";
            AssetDatabase.CreateAsset(newItem, assetPath);
            AssetDatabase.SaveAssets();

            GameObject assetRoot = objs[i] as GameObject;
            string prefabPath = AssetDatabase.GetAssetPath(assetRoot);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            // var rend = prefabRoot.GetComponentInChildren<Renderer>();
            // var equip = prefabRoot.GetComponent<EquipmentObject>();
            // if(!equip) equip = prefabRoot.AddComponent<EquipmentObject>();
            // equip.equipItem = newItem;
            // equip.equipItem = AssetDatabase.LoadAssetAtPath<Item>(assetPath);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    static string GetGUID(GameObject obj) {
        string guid;
        long l;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out l);
        return guid;
    }
}
#endif
}