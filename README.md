# Basic Runtime Building System

### Dependencies
- https://github.com/khncao/com.minus4kelvin.core 
- Tested working on Unity 2020.3.6f1+

### Buildable Prefab Hierarchy
- Pivot root
  - Child with BuildingSystemObject, Renderer, Collider, Rigidbody
    - Other child game objects, Renderer, Collider
  - Other child game objects

### Usage
- Create buildable item with prefab reference manually or with generator script
- Call BuildingSystem SetBuildObject(ItemBuildable) or ItemBuildable SingleClick()

### Todo
- Prefabs, example
- Contextual buildable snapping
- Configurable buildable itemization
- Optimizations (material, baking, lighting, etc)

