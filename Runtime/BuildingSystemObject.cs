using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using m4k.InventorySystem;

namespace m4k.BuildSystem {
// [RequireComponent(typeof(Rigidbody))]
public class BuildingSystemObject : MonoBehaviour, IBuildable
{
    public Item item;
    public Collider[] cols;
    // public Renderer[] renderers;
    public Rigidbody rb;
    public Renderer[] buildingVisuals;

    int[] origColLayers;
    bool initialized;
    bool beingBuilt;
    MaterialsReplacer materialsReplacer;
    LayerMask charLayer;
    // Material[][] origMats, validMats, invalidMats;
    NavMeshObstacle[] navObstacles;
    BuildingSystem buildingSystem;

    public void Initialize(BuildingSystem bs) {
        if(!initialized) {
            buildingSystem = bs;
            
            if(!item)
                item = buildingSystem.currItem;
            charLayer = LayerMask.NameToLayer("Character");
            cols = GetComponentsInChildren<Collider>();
            origColLayers = new int[cols.Length];
            materialsReplacer = GetComponent<MaterialsReplacer>();
            if(!materialsReplacer)
                materialsReplacer = gameObject.AddComponent<MaterialsReplacer>();
            materialsReplacer.RegisterReplacementMat(new Material[] {buildingSystem.invalidPlacementMat, buildingSystem.validPlacementMat});
            // renderers = GetComponentsInChildren<Renderer>();
            // validMats = new Material[renderers.Length][];
            // invalidMats = new Material[renderers.Length][];
            // origMats = new Material[renderers.Length][];

            // for(int i = 0; i < renderers.Length; ++i) {
            //     validMats[i] = new Material[renderers[i].materials.Length];
            //     invalidMats[i] = new Material[renderers[i].materials.Length];
            //     for(int j = 0; j < renderers[i].materials.Length; ++j) {
            //         validMats[i][j] = buildingSystem.validPlacementMat;
            //         invalidMats[i][j] = buildingSystem.invalidPlacementMat;
            //     }
            // }
            navObstacles = GetComponentsInChildren<NavMeshObstacle>();

            initialized = true;
        }
        StartPlacement();
    }
    void StartPlacement() {
        beingBuilt = true;

        for(int i = 0; i < cols.Length; ++i) {
            origColLayers[i] = cols[i].gameObject.layer;

            if(item.HasTag(ItemTag.Zone) && origColLayers[i] != buildingSystem.builtLayer) {
                // cols[i].gameObject.layer = buildingSystem.triggerLayer;
            }
            else {
                cols[i].gameObject.layer = buildingSystem.buildingLayer;
                cols[i].isTrigger = true;
            }
        }
        for(int i = 0; i < navObstacles.Length; ++i) {
            navObstacles[i].enabled = false;
        }
        // for(int i = 0; i < renderers.Length; ++i) {
        //     origMats[i] = renderers[i].materials;
        //     renderers[i].materials = validMats[i];
        // }
        materialsReplacer.Replace(buildingSystem.validPlacementMat);
        rb = GetComponentInChildren<Rigidbody>();
        if(!rb)
            rb = cols[0].gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        // rb.detectCollisions = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }
    public void FinalizePlacement() {
        for(int i = 0; i < cols.Length; ++i) {
            if(item.HasTag(ItemTag.Zone) && origColLayers[i] != buildingSystem.builtLayer) {
                // cols[i].gameObject.layer = buildingSystem.triggerLayer;
            }
            else {
                cols[i].isTrigger = false;
                if(i > 0 && cols[i].gameObject == cols[i-1].gameObject) {}
                else
                    cols[i].gameObject.layer = origColLayers[i];
            }
        }
        Destroy(rb);
        // rb.isKinematic = true;
        // rb.detectCollisions = false;

        // for(int i = 0; i < renderers.Length; ++i) {
        //     renderers[i].materials = origMats[i];
        // }
        materialsReplacer.Restore();
        for(int i = 0; i < navObstacles.Length; ++i) {
            navObstacles[i].enabled = true;
        }
        // var table = GetComponentInParent<TableController>();
        // if(table) table.RegisterTable();

        beingBuilt = false;
    }
    // private void OnTriggerEnter(Collider other) {
    //     buildingSystem.SetBuildObjectColliding(true);
    // }
    bool prevOverlapping;
    public void SetOverlapping(bool overlapping) {
        if(overlapping != prevOverlapping) {
            if(overlapping) {
                // for(int i = 0; i < renderers.Length; ++i) {
                //     renderers[i].materials = invalidMats[i];
                // }
                materialsReplacer.Replace(buildingSystem.invalidPlacementMat);
            }
            else {
                // for(int i = 0; i < renderers.Length; ++i) {
                //     renderers[i].materials = validMats[i];
                // }
                materialsReplacer.Replace(buildingSystem.validPlacementMat);
            }
            prevOverlapping = overlapping;
        }
    }
    
    private void OnTriggerExit(Collider other) {
        if(!beingBuilt) return;
        buildingSystem.SetBuildObjectColliding(false, false, other);
        SetOverlapping(false);
    }
    private void OnTriggerStay(Collider other) {
        if(!beingBuilt) return;
        if(item.HasTag(ItemTag.Zone) && (other.gameObject.layer ==  charLayer || other.CompareTag("Floor"))) return;
        buildingSystem.SetBuildObjectColliding(true, other.CompareTag(gameObject.tag), other);
        // Debug.Log(other.gameObject);
        SetOverlapping(true);

        // Debug.Log("Triggerstay");
    }
    // private void OnCollisionEnter(Collision other) {
    //     buildingSystem.SetBuildObjectColliding(true);
    // }
    // private void OnCollisionExit(Collision other) {
    //     buildingSystem.SetBuildObjectColliding(false);
    // }

    public void OnToggleBuildableVisual(bool b) {
        foreach(var r in buildingVisuals)
            r.enabled = b;
    }
    public void OnToggleBuildableEdit(bool b) {
        
    }
}
}