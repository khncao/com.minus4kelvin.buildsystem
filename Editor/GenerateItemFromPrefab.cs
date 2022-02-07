using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using m4k.Items;


namespace m4k.BuildSystem {
#if UNITY_EDITOR
public class GenerateItemFromPrefab : EditorWindow
{
    public string buildItemOutputPath = "Assets/Data/Buildables/";
    public int buildableLayer;
    public ItemTag itemTag;
    public int maxAmount = 999;
    
    [MenuItem("Tools/Items From Prefabs/Buildable")]
    static void ShowWindow() {
        var window = GetWindow<GenerateItemFromPrefab>();
    }

    void OnGUI() {
        EditorGUILayout.HelpBox("Select prefab(s) in project window to generate corresponding items with prefab and presets assigned.\nDesignates first found child collider from prefab root as buildable main object", MessageType.Info);

        buildItemOutputPath = EditorGUILayout.TextField("Item save path", buildItemOutputPath);
        buildableLayer = EditorGUILayout.LayerField("Buildable Layer", buildableLayer);
        itemTag = (ItemTag)EditorGUILayout.EnumPopup("Item tag", itemTag);
        maxAmount = EditorGUILayout.IntField("Max amount", maxAmount);

        if(GUILayout.Button("Generate"))
            CreateBuildableItems();
    }
    
    void CreateBuildableItems() {
        var objs = Selection.gameObjects;

        for(int i = 0; i < objs.Length; ++i) {
            if(objs[i].scene.IsValid()) {
                Debug.LogWarning($"{objs[i].name} invalid hierarchy selection");
                continue;
            }
            var newItem = ScriptableObject.CreateInstance<ItemBuildable>();
            newItem.displayName = objs[i].name;
            newItem.itemType = ItemType.Buildable;
            newItem.itemTags = new List<ItemTag>();
            newItem.itemTags.Add(itemTag);
            newItem.maxAmount = maxAmount;
            newItem.prefab = objs[i];
            // newItem.prefabRef = new UnityEngine.AddressableAssets.AssetReferenceT<GameObject>(GetGUID(objs[i])); 

            string assetPath = buildItemOutputPath + objs[i].name + ".asset";
            AssetDatabase.CreateAsset(newItem, assetPath);
            AssetDatabase.SaveAssets();

            GameObject assetRoot = objs[i] as GameObject;
            string prefabPath = AssetDatabase.GetAssetPath(assetRoot);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            var col = prefabRoot.GetComponentInChildren<Collider>();
            if(!col) {
                Debug.LogWarning($"{objs[i].name} item generated but collider not found");
                continue;
            }
            var builtComponent = col.GetComponent<BuildingSystemObject>();
            if(!builtComponent) {
                builtComponent = col.gameObject.AddComponent<BuildingSystemObject>();
            }

            builtComponent.item = AssetDatabase.LoadAssetAtPath<ItemBuildable>(assetPath);
            col.gameObject.layer = buildableLayer;

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            Debug.Log($"{objs[i].name} buildable generated");
        }
    }

    // static string GetGUID(GameObject obj) {
    //     string guid;
    //     long l;
    //     AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out l);
    //     return guid;
    // }
}
#endif
}