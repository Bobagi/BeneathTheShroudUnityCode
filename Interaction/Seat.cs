using UnityEngine;

// A chair or armchair the player can sit in (E). The builder measures where the seat is and which way
// it faces from the model itself (Configure); the player turns, steps in front of it and sits down.
public sealed class Seat : Interactable
{
    [SerializeField]
    private Vector3 seatLocal;

    [SerializeField]
    private Vector3 facingLocal = Vector3.forward;

    public Vector3 SeatPoint => transform.TransformPoint(seatLocal);

    public Vector3 Facing
    {
        get
        {
            Vector3 f = Vector3.ProjectOnPlane(transform.TransformDirection(facingLocal), Vector3.up);
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }
    }

    // The prop's root sits on the floor.
    public float FloorY => transform.position.y;

    public void Configure(Vector3 seatWorld, Vector3 facingWorld)
    {
        seatLocal = transform.InverseTransformPoint(seatWorld);
        facingLocal = transform.InverseTransformDirection(facingWorld);
    }

    public override void Interact(PlayerInteractor interactor)
    {
        interactor.Controller.SitOn(this);
    }
}
