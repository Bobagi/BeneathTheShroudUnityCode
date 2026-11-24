using UnityEngine;

public sealed class MoveUpWithRigidbody : MonoBehaviour
{
    [SerializeField]
    private float upwardForceMagnitude = 10f;

    private Rigidbody attachedRigidbody;

    private void Awake()
    {
        attachedRigidbody = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        attachedRigidbody.AddForce(Vector3.up * upwardForceMagnitude, ForceMode.Force);
    }
}
