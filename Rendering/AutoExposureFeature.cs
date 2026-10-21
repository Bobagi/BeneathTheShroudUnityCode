using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// Eye adaptation for URP, which has none (04/10, Gustavo: "the bloom is weird all the time in gameplay"). Without it a
// lamp or the torch on a near wall stayed at the brightness the scene was graded for, and bloomed white. For every
// camera that carries an AutoExposure component, before post-processing:
//   1. the HDR frame is metered into a 64 x 36 target of mean log2 luminance (Hidden/Horror/AutoExposure, pass 0);
//   2. that target is read back to the CPU a frame or two later (AsyncGPUReadback, no stall) and AutoExposure turns
//      it into an exposure that eases toward the target like an eye (fast to close, slow to open);
//   3. the frame is multiplied by the exposure (pass 1) and becomes the camera colour, so bloom and the tone mapping
//      see what the eye sees. Cost: two small full-screen passes.
public sealed class AutoExposureFeature : ScriptableRendererFeature
{
    public const int MeterWidth = 64;
    public const int MeterHeight = 36;

    [SerializeField]
    private Shader shader;

    private Material material;
    private AutoExposurePass pass;

    public override void Create()
    {
        if (shader == null)
        {
            shader = Shader.Find("Hidden/Horror/AutoExposure");
        }

        pass = new AutoExposurePass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (shader == null || renderingData.cameraData.cameraType != CameraType.Game)
        {
            return;
        }

        AutoExposure eye = AutoExposure.For(renderingData.cameraData.camera);
        if (eye == null || !eye.isActiveAndEnabled)
        {
            return;
        }

        if (material == null)
        {
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        pass.Setup(material, eye);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
    }

    private sealed class AutoExposurePass : ScriptableRenderPass
    {
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int MeterSizeId = Shader.PropertyToID("_MeterSize");
        private readonly Dictionary<AutoExposure, Material> perCamera = new Dictionary<AutoExposure, Material>();
        private Material shared;
        private AutoExposure eye;

        public void Setup(Material material, AutoExposure eye)
        {
            shared = material;
            this.eye = eye;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
            {
                return;
            }

            // A material per camera, so each camera's exposure is the one its pass draws with.
            if (!perCamera.TryGetValue(eye, out Material material) || material == null)
            {
                material = new Material(shared) { hideFlags = HideFlags.HideAndDontSave };
                perCamera[eye] = material;
            }

            TextureHandle colour = resources.activeColorTexture;
            RTHandle meter = eye.MeterTarget(MeterWidth, MeterHeight);
            material.SetVector(MeterSizeId, new Vector4(MeterWidth, MeterHeight, 0f, 0f));
            material.SetFloat(ExposureId, eye.Exposure);

            TextureHandle meterHandle = renderGraph.ImportTexture(meter);
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(colour, meterHandle, material, 0), "Auto exposure: meter");

            TextureDesc desc = renderGraph.GetTextureDesc(colour);
            desc.name = "AutoExposedColour";
            desc.clearBuffer = false;
            TextureHandle exposed = renderGraph.CreateTexture(desc);
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(colour, exposed, material, 1), "Auto exposure: apply");
            resources.cameraColor = exposed;
            eye.RequestReadback();
        }
    }
}
