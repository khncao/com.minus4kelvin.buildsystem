

using UnityEngine;
using UnityEngine.Events;

namespace m4k.BuildSystem {
public class BuildableComponent : MonoBehaviour, IBuildable {
    public UnityEvent<bool> onToggleVisuals, onToggleEdit;

    public void OnToggleBuildableVisual(bool b) {
        onToggleVisuals?.Invoke(b);
    }
    public void OnToggleBuildableEdit(bool b) {
        onToggleEdit?.Invoke(b);
    }
}
}