using UnityEngine;

// On every collider of a creature's ragdoll: which part of the body it is, so a shot to the head counts for
// more than one to a hand, and which bone it moves (the reaction pushes that bone). Forwards the shot to the
// creature that owns it.
public sealed class CreatureHitbox : MonoBehaviour, IBulletTarget
{
    public BodyRegion region = BodyRegion.Torso;
    public CreatureVitals owner;
    public Rigidbody body;

    public float DamageScale
    {
        get
        {
            switch (region)
            {
                case BodyRegion.Head: return 3.2f;
                case BodyRegion.Torso: return 1f;
                case BodyRegion.Arm: return 0.55f;
                default: return 0.7f;
            }
        }
    }

    public void TakeShot(Shot shot)
    {
        if (owner != null)
        {
            owner.TakeShot(this, shot);
        }
    }
}
