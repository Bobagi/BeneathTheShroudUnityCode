using System;
using UnityEngine;

// Something the revolver can hit and react to (bottles, the creature, props).
public interface IShootable
{
    void OnShot(Vector3 point, Vector3 direction);
}

// Tiny global event hub so world systems (the creature, ambience) can react to the player's noise
// without hard references.
public static class WorldEvents
{
    public static event Action<Vector3, float> Noise;

    public static void RaiseNoise(Vector3 position, float radius)
    {
        Noise?.Invoke(position, radius);
    }
}
