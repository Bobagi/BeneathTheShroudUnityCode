using UnityEngine;

// Rain around whatever camera is rendering (the cutscene cuts across a whole island and the sea): the emitter
// sits a few metres above the main camera and the drops fall in world space, so moving the camera never drags
// the rain along with it.
[RequireComponent(typeof(ParticleSystem))]
public sealed class CameraRain : MonoBehaviour
{
    [SerializeField]
    private float heightAbove = 10f;

    private Transform view;

    private void LateUpdate()
    {
        if (view == null)
        {
            Camera main = Camera.main;
            if (main == null)
            {
                return;
            }

            view = main.transform;
        }

        transform.position = view.position + Vector3.up * heightAbove + view.forward * 6f;
    }
}
