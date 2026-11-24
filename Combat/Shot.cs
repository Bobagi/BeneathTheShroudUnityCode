using UnityEngine;

// One bullet striking something: where, from which way, how hard. Damage is the weapon's base damage; the
// thing hit scales it by the body region (CreatureHitbox).
public struct Shot
{
    public Vector3 point;
    public Vector3 direction;
    public Vector3 normal;
    public float damage;
    // Impulse given to a body when it goes limp (N*s): a .357 round dumps a few N*s into flesh.
    public float impulse;
    public Collider collider;
}

// Anything a bullet can affect: a creature's hitbox, a prop.
public interface IBulletTarget
{
    void TakeShot(Shot shot);
}

public enum BodyRegion
{
    Head,
    Torso,
    Arm,
    Leg,
}
