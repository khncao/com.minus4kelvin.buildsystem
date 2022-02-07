using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;
using m4k.Items;

namespace m4k.BuildSystem {
public class BuildingSystemObject : MonoBehaviour, IBuildable
{
    [Header("Rb and cols should be on or in children of this object.\nThis object should be direct child of prefab root")]
    public Item item;
    public bool allowEdit = true;
    public bool keepActiveRb = false;
    [Header("Specific ignores for collision detection")]
    public LayerMask ignoreLayers;
    public List<string> ignoreTags;
    [Header("Visuals are build mode only markers such as directional")]
    public Transform buildingVisualsParent;
    [Header("Optional for aux serialize(inventory)")]
    public GuidComponent guidComponent;
    public UnityEvent<bool> onToggleVisuals, onToggleEdit;

    public BuildingSystem.BuiltItem builtItem { get; set; }

    int[] origColLayers;
    bool[] origColTriggers;
    bool initialized;
    bool beingBuilt;
    MaterialsReplacer materialsReplacer;
    NavMeshObstacle[] navObstacles;
    BuildingSystem buildingSystem;
    Renderer[] buildingVisuals;
    Collider[] cols;
    Rigidbody rb;

    /// <summary>
    /// Should be called once before starting placement. Initialize references
    /// </summary>
    /// <param name="bs"></param>
    /// <param name="builtItem"></param>
    public void Initialize(BuildingSystem bs, BuildingSystem.BuiltItem builtItem) {
        if(!initialized) {
            buildingSystem = bs;
            
            this.builtItem = builtItem;
            if(!item)
                item = buildingSystem.currItem;

            cols = GetComponentsInChildren<Collider>();
            origColLayers = new int[cols.Length];
            origColTriggers = new bool[cols.Length];

            materialsReplacer = GetComponent<MaterialsReplacer>();
            if(!materialsReplacer)
                materialsReplacer = gameObject.AddComponent<MaterialsReplacer>();
            materialsReplacer.RegisterReplacementMat(new Material[] {buildingSystem.invalidPlacementMat, buildingSystem.validPlacementMat});

            navObstacles = GetComponentsInChildren<NavMeshObstacle>();

            if(buildingVisualsParent)
                buildingVisuals = buildingVisualsParent.GetComponentsInChildren<Renderer>();

            initialized = true;
        }
    }

    /// <summary>
    /// Begin object placement mode. Relevant state is cached for restoration. Layers are set to specific building layer, colliders are flagged as triggers. NavmeshObstacles are disabled. Materials are initially set to valid. Rigidbody is found or added, then initialized for building.
    /// </summary>
    public void StartPlacement() {
        beingBuilt = true;
        bool builtLayerFound = false;

        for(int i = 0; i < cols.Length; ++i) {
            origColLayers[i] = cols[i].gameObject.layer;
            origColTriggers[i] = cols[i].isTrigger;

            if(cols[i].gameObject.layer == buildingSystem.builtLayer)
                builtLayerFound = true;

            cols[i].gameObject.layer = buildingSystem.buildingLayer;
            cols[i].isTrigger = true;
        }
        if(!builtLayerFound) {
            Debug.LogWarning("No original colliders have 'built' layer; object may not be editable");
        }

        for(int i = 0; i < navObstacles.Length; ++i) {
            navObstacles[i].enabled = false;
        }

        materialsReplacer.Replace(buildingSystem.validPlacementMat);

        rb = GetComponentInChildren<Rigidbody>();
        if(!rb)
            rb = cols[0].gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    /// <summary>
    /// Set object to finalized built state. Restore collider and layer states. Rigidbody handled based on options. Materials restored and NavmeshObstacles enabled.
    /// </summary>
    public void FinalizePlacement() {
        if(!beingBuilt) 
            return;

        for(int i = 0; i < cols.Length; ++i) {
            cols[i].isTrigger = origColTriggers[i];
            cols[i].gameObject.layer = origColLayers[i];
        }
        if(keepActiveRb) {
            rb.isKinematic = false;
        }
        else {
            Destroy(rb);
        }
        
        materialsReplacer.Restore();
        for(int i = 0; i < navObstacles.Length; ++i) {
            navObstacles[i].enabled = true;
        }

        beingBuilt = false;
    }

    bool prevOverlapping;
    void SetOverlapping(bool overlapping) {
        if(overlapping != prevOverlapping) {
            if(overlapping) {
                materialsReplacer.Replace(buildingSystem.invalidPlacementMat);
            }
            else {
                materialsReplacer.Replace(buildingSystem.validPlacementMat);
            }
            prevOverlapping = overlapping;
        }
    }
    
    private void OnTriggerExit(Collider other) {
        if(!beingBuilt) 
            return;

        buildingSystem.SetBuildObjectColliding(false, false, other);
        SetOverlapping(false);
    }

    private void OnTriggerStay(Collider other) {
        if(!beingBuilt)
            return;
        if((ignoreLayers & (1 << other.gameObject.layer)) != 0
        || ignoreTags.Contains(other.tag))
            return;

        buildingSystem.SetBuildObjectColliding(true, other.CompareTag(gameObject.tag), other);
        SetOverlapping(true);
    }

    public void OnToggleBuildableVisual(bool b) {
        if(buildingVisuals == null) 
            return;

        foreach(var r in buildingVisuals)
            r.enabled = b;
            
        onToggleVisuals?.Invoke(b);
    }

    public void OnToggleBuildableEdit(bool b) {
        onToggleEdit?.Invoke(b);
    }

    public bool CanEdit() {
        return allowEdit;
    }
}
}