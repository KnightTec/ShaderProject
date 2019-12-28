using UnityEngine;


//https://bartwronski.files.wordpress.com/2014/08/bwronski_volumetric_fog_siggraph2014.pdf

[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public class VolumetricFogRenderer : MonoBehaviour
{
    [Range(0, 1)]
    public float scattering = 0.1f;
    [Range(-1, 1)]
    public float anisotropy = 0;
    public float fogHeight;
    [Range(0.01f, 1)]
    public float fogFalloff;

    public DirectionalLightData directionalLightData;
    public ComputeShader densityLightingShader;
    public ComputeShader scatteringShader;
    public ComputeShader applyFogShader;
    public Shader applyFogShader0;

    private RenderTexture tempDestination;
    private int densityLightingKernel;
    private int scatteringKernel;
    private int applyFogKernel;
    private Material applyFogMaterial;

    private Camera cam;
    private Matrix4x4 viewProjectionMatrixInverse;
    private Vector4 resolution;
    private RenderTexture fogVolume;
    private RenderTexture accumulatedFogVolume;
    private Vector4[] frustumRays;

    void OnEnable()
    {
        GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth;
        cam = GetComponent<Camera>();
        applyFogMaterial = new Material(applyFogShader0);
    }

    void Start()
    {
        densityLightingKernel = densityLightingShader.FindKernel("CSMain");
        scatteringKernel = scatteringShader.FindKernel("CSMain");
        applyFogKernel = applyFogShader.FindKernel("CSMain");
        resolution = new Vector4();
        frustumRays = new Vector4[4];
    }

    void OnDestroy()
    {
        if (null != tempDestination)
        {
            tempDestination.Release();
            tempDestination = null;
        }
    }

    private void OnPreRender()
    {
        directionalLightData.updateData();
    }

    private void calculateFrustumRays()
    {
        Vector3[] frustumRaysTmp = new Vector3[4];
        cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), 1, Camera.MonoOrStereoscopicEye.Mono, frustumRaysTmp);
        for (int i = 0; i < 4; i++)
        {
            Vector3 worldSpaceRay = cam.transform.TransformVector(frustumRaysTmp[i]);
            frustumRays[i] = worldSpaceRay;
            // i = 0 -> lower left
            // i = 1 -> upper left
            // i = 2 -> upper right
            // i = 3 -> lower right
        }
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // recreate texture if size was changed
        if (null == tempDestination || source.width != tempDestination.width
           || source.height != tempDestination.height)
        {
            if (null != tempDestination)
            {
                tempDestination.Release();
            }
            tempDestination = new RenderTexture(source.width, source.height,
               source.depth);
            tempDestination.enableRandomWrite = true;
            tempDestination.format = RenderTextureFormat.ARGB64;
            tempDestination.Create();

            resolution.x = source.width;
            resolution.y = source.height;
            resolution.z = 1.0f / source.width;
            resolution.w = 1.0f / source.height;
        }
        if (fogVolume == null)
        {
            fogVolume = new RenderTexture(160, 90, 0, RenderTextureFormat.ARGBHalf);
            fogVolume.enableRandomWrite = true;
            fogVolume.volumeDepth = 128;
            fogVolume.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            fogVolume.Create();
        }
        if (accumulatedFogVolume == null)
        {
            accumulatedFogVolume = new RenderTexture(160, 90, 0, RenderTextureFormat.ARGBHalf);
            accumulatedFogVolume.enableRandomWrite = true;
            accumulatedFogVolume.volumeDepth = 128;
            accumulatedFogVolume.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            accumulatedFogVolume.Create();
        }

        // compute all required variables
        Matrix4x4 viewMat = cam.worldToCameraMatrix;
        Matrix4x4 projMat = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
        viewProjectionMatrixInverse = projMat * viewMat;
        viewProjectionMatrixInverse = viewProjectionMatrixInverse.inverse;

        calculateFrustumRays();

        //TODO: use command buffers instead of OnRenderImage (performance??)
        //TODO: fix depth texture delay in forward

        //TODO: shadow filtering: convert to exponential shadow maps
        //TODO: logarithmic depth slice distribution (right now its linear)
        // -> see formula one slide 5 http://advances.realtimerendering.com/s2016/Siggraph2016_idTech6.pdf
        //TODO: release buffers correctly (might be the reason for some crashes)


        directionalLightData.setComputeBuffer(densityLightingKernel, densityLightingShader);
        densityLightingShader.SetVector("cameraPosition", transform.position);
        densityLightingShader.SetTextureFromGlobal(densityLightingKernel, "cascadeShadowMap", "_CascadeShadowMapCopy");
        densityLightingShader.SetTexture(densityLightingKernel, "fogVolume", fogVolume);
        densityLightingShader.SetVectorArray("frustumRays", frustumRays);
        densityLightingShader.SetFloat("nearPlane", cam.nearClipPlane);
        densityLightingShader.SetFloat("farPlane", cam.farClipPlane);
        densityLightingShader.SetFloat("scattering", scattering);
        densityLightingShader.SetFloat("g", anisotropy);
        densityLightingShader.SetFloat("fogHeight", fogHeight);
        densityLightingShader.SetFloat("fogFalloff", fogFalloff);
        densityLightingShader.Dispatch(densityLightingKernel, 40, 24, 32);

        scatteringShader.SetTexture(scatteringKernel, "accumulatedFogVolume", accumulatedFogVolume);
        scatteringShader.SetTexture(scatteringKernel, "fogVolume", fogVolume);
        scatteringShader.SetFloat("sliceDepth", (cam.farClipPlane - cam.nearClipPlane) / 128.0f);
        scatteringShader.Dispatch(scatteringKernel, 20, 12, 1);

        applyFogShader.SetVector("resolution", resolution);
        applyFogShader.SetTextureFromGlobal(applyFogKernel, "depth", "_CameraDepthTexture");
        applyFogShader.SetTexture(applyFogKernel, "source", source);
        applyFogShader.SetTexture(applyFogKernel, "result", tempDestination);
        applyFogShader.SetTexture(applyFogKernel, "accumulatedFogVolume", accumulatedFogVolume);
        applyFogShader.Dispatch(applyFogKernel, (tempDestination.width + 7) / 8, (tempDestination.height + 7) / 8, 1);

        //applyFogMaterial.SetTexture("mainTex", source);
        //applyFogMaterial.SetTexture("froxelVolume", fogVolume);
        //Graphics.Blit(tempDestination, destination, applyFogMaterial);
        Graphics.Blit(tempDestination, destination);
    }
}
