using UnityEngine;

// Optional per-item logic (revolver firing, lantern light). Lives on the WorldItem's GameObject and
// is driven by PlayerInventory while the item is in the hand.
public abstract class HeldItemBehaviour : MonoBehaviour
{
    protected PlayerInventory Owner { get; private set; }
    protected bool IsRaised { get; private set; }

    public virtual string StatusText => string.Empty;

    public void NotifyEquipped(PlayerInventory owner)
    {
        Owner = owner;
        OnEquipped();
    }

    public void NotifyUnequipped()
    {
        IsRaised = false;
        OnUnequipped();
        Owner = null;
    }

    public void NotifyRaised(bool raised)
    {
        if (IsRaised == raised)
        {
            return;
        }

        IsRaised = raised;
        OnRaisedChanged(raised);
    }

    protected virtual void OnEquipped() { }

    protected virtual void OnUnequipped() { }

    protected virtual void OnRaisedChanged(bool raised) { }

    // Primary action (left mouse / right trigger). aimRay is the camera ray through the reticle.
    public virtual void OnPrimaryAction(Ray aimRay, Vector3 aimPoint) { }

    public virtual void OnReloadAction() { }

    public virtual void OnToggleAction() { }
}
