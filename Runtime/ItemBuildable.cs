
using UnityEditor;
using UnityEngine;
using m4k.InventorySystem;

namespace m4k.BuildSystem {
[CreateAssetMenu(fileName = "ItemBuildable", menuName = "ScriptableObjects/ItemBuildable")]
public class ItemBuildable : Item {
    // [Header("Buildable")]
    public override void SingleClick(ItemSlot slot)
    {
        base.SingleClick(slot);
        BuildingSystem.I.SetBuildObject(this);
    }

    public override void AddToInventory(int amount, bool notify)
    {
        // base.AddToInventory(amount, notify);
        BuildingSystem.I.AddBuildItem(this, amount);
    }
    
}
}