using UnityEngine;

// On every bone collider of a tentacled creature: which chain and which bone of it, so the shot throws back the
// right arm from the right place (TentacleRig.Hit).
public sealed class TentacleHitbox : MonoBehaviour, IBulletTarget
{
    public TentacleRig rig;
    public int chain;
    public int bone;

    public void TakeShot(Shot shot)
    {
        if (rig != null)
        {
            rig.Hit(chain, bone, shot);
        }
    }
}
