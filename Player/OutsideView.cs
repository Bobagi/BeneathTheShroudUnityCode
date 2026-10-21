using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// A camera in front of the player that films the body from outside (test scenes: the first person view only
// shows chest, arms and legs). It follows the view's heading with a lag and, only while it renders, draws the
// renderers that are shadow-only in first person (the head with its eyes, the hair and beard, the suit's hood
// and mask). The head is the body's own, shared by every skin (07/10), so nothing is swapped.
[RequireComponent(typeof(Camera))]
public sealed class OutsideView : MonoBehaviour
{
    [SerializeField]
    private Transform player;

    [SerializeField]
    private Transform body;

    [SerializeField]
    private float distance = 2.6f;

    [SerializeField]
    private float height = 1.35f;

    [SerializeField]
    private float lookHeight = 1.0f;

    [SerializeField]
    private float follow = 3f;

    private readonly List<Renderer> hidden = new List<Renderer>();
    private Camera view;
    private float yaw;

    public RenderTexture Target { get; private set; }

    private void Awake()
    {
        view = GetComponent<Camera>();
        Target = new RenderTexture(600, 800, 24) { name = "OutsideView" };
        view.targetTexture = Target;
        foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
        {
            if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
            {
                hidden.Add(r);
            }
        }

        yaw = player.eulerAngles.y;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
    }

    private void OnDestroy()
    {
        if (Target != null)
        {
            Target.Release();
        }
    }

    private void LateUpdate()
    {
        yaw = Mathf.LerpAngle(yaw, player.eulerAngles.y, 1f - Mathf.Exp(-follow * Time.deltaTime));
        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
        Vector3 feet = body.position;
        transform.position = feet + heading * new Vector3(0f, height, distance);
        transform.rotation = Quaternion.LookRotation(feet + Vector3.up * lookHeight - transform.position);
    }

    private void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera == view)
        {
            SetHidden(ShadowCastingMode.On);
        }
    }

    private void EndCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera == view)
        {
            SetHidden(ShadowCastingMode.ShadowsOnly);
        }
    }

    private void SetHidden(ShadowCastingMode mode)
    {
        foreach (Renderer r in hidden)
        {
            if (r != null)
            {
                r.shadowCastingMode = mode;
            }
        }
    }
}
