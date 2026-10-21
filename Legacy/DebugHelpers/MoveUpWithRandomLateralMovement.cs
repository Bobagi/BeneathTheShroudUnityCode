using UnityEngine;

public sealed class MoveUpWithRandomLateralMovement : MonoBehaviour
{
    [SerializeField]
    private float upwardForceMagnitude = 10f;

    [SerializeField]
    private float lateralForceMagnitude = 5f;

    [SerializeField]
    private float lateralDirectionChangeIntervalSeconds = 0.5f;

    private Rigidbody attachedRigidbody;
    private Vector3 currentLateralDirection;
    private float nextLateralDirectionChangeTime;

    private void Awake()
    {
        attachedRigidbody = GetComponent<Rigidbody>();
        currentLateralDirection = GenerateRandomLateralDirection();
        nextLateralDirectionChangeTime = Time.time + lateralDirectionChangeIntervalSeconds;
    }

    private void FixedUpdate()
    {
        if (Time.time >= nextLateralDirectionChangeTime)
        {
            currentLateralDirection = GenerateRandomLateralDirection();
            nextLateralDirectionChangeTime = Time.time + lateralDirectionChangeIntervalSeconds;
        }

        Vector3 upwardForceVector = Vector3.up * upwardForceMagnitude;
        Vector3 lateralForceVector = currentLateralDirection * lateralForceMagnitude;

        attachedRigidbody.AddForce(upwardForceVector + lateralForceVector, ForceMode.Force);
    }

    private static Vector3 GenerateRandomLateralDirection()
    {
        Vector2 randomDirection2D = Random.insideUnitCircle.normalized;
        return new Vector3(randomDirection2D.x, 0f, randomDirection2D.y);
    }
}
