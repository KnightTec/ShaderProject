using UnityEngine;


//https://bartwronski.files.wordpress.com/2014/08/bwronski_volumetric_fog_siggraph2014.pdf

[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public class VolumetricFogRenderer : MonoBehaviour
{
    [Range(0, 1)]
    public float scattering = 0.1f;
    public Color scatterColor = Color.white;
    [Range(-0.99f, 0.99f)]
    public float anisotropy = 0;
    [Range(0, 1)]
    public float transmittance = 0;
    public float fogHeight;
    [Range(0.01f, 1)]
    public float fogFalloff;
    [Range(0, 1)]
    public float noiseIntensity = 1;
    [Range(0.1f, 100)]
    public float noiseSize = 8;
    public Vector3 noiseDirection;

    public DirectionalLightData directionalLightData;
    public ComputeShader densityLightingShader;
    public ComputeShader tssBlendShader;
    public ComputeShader scatteringShader;
    public ComputeShader applyFogShader;
    public Shader applyFogShader0;
    public float jitterStrength = 1;
    public Color ambientColor;

    private RenderTexture tempDestination;

    private int densityLightingKernel;
    private int tssBlendKernel;
    private int scatteringKernel;
    private int applyFogKernel;
    private Material applyFogMaterial;

    private Camera cam;
    private Vector4 resolution;

    private RenderTexture fogVolume0;
    private RenderTexture fogVolume1;
    private RenderTexture fogVolume2;
    private RenderTexture currentfogVolume;
    private RenderTexture historyFogVolume;
    private RenderTexture blendedFogVolume;
    private ComputeBuffer pointLightBuffer;

    private RenderTexture accumulatedFogVolume;
    private Vector4[] frustumRays;
    private float[] sliceDepths;
    private float[][] jitteredSliceDepths;
    private int jitterIndex = 0;
   
    struct FogPointLight
    {
        public Vector4 position;
        public Vector4 color;
        public float range;
        public float intensity;
    };
    private FogPointLight[] pointLights;
    private int pointLightCount;

    private const int depthSliceCount = 128;

    void OnEnable()
    {
        GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth;
        cam = GetComponent<Camera>();
        applyFogMaterial = new Material(applyFogShader0);
    }

    void Start()
    {
        densityLightingKernel = densityLightingShader.FindKernel("CSMain");
        tssBlendKernel = tssBlendShader.FindKernel("CSMain");
        scatteringKernel = scatteringShader.FindKernel("CSMain");
        applyFogKernel = applyFogShader.FindKernel("CSMain");
        resolution = new Vector4();
        frustumRays = new Vector4[4];
        sliceDepths = new float[depthSliceCount];
        jitteredSliceDepths = new float[2][];
        calculateSliceDepths();
        pointLightBuffer = new ComputeBuffer(64, 40);
    }

    void OnDestroy()
    {
        if (null != tempDestination)
        {
            tempDestination.Release();
            tempDestination = null;
        }
    }

    private void Update()
    {
        if (jitterIndex % 2 == 0)
        {
            currentfogVolume = fogVolume0;
            historyFogVolume = fogVolume1;
            blendedFogVolume = fogVolume2;
        }
        else
        {
            currentfogVolume = fogVolume1;
            historyFogVolume = fogVolume0;
            blendedFogVolume = fogVolume2;
        }
        jitterIndex = (jitterIndex + 1) % 2;
    }

    private void OnPreRender()
    {
        directionalLightData.updateData();
        getPointLightData();
    }
    
    private static float haltonSequence(int i, int b)
    {
        float f = 1;
        float r = 0;
        while (i > 0)
        {
            f /= b;
            r += f * (i % b);
            i /= b;
        }
        return r;
    }

    private void calculateSliceDepths()
    {
        float farOverNear = cam.farClipPlane / cam.nearClipPlane;
        for (int i = 0; i < depthSliceCount; i++)
        {
            sliceDepths[i] = cam.nearClipPlane * Mathf.Pow(farOverNear, i / (float)depthSliceCount);
        }
        float sliceProportion = Mathf.Pow(farOverNear, 1 / (float)depthSliceCount) - 1;
        for (int j = 0; j < 2; j++)
        {
            jitteredSliceDepths[j] = new float[depthSliceCount];
            float offset;
            if (j == 0)
            {
                offset = -0.5f;
            } else
            {
                offset = 0.5f;
            }
            offset *= jitterStrength;
            for (int i = 0; i < depthSliceCount; i++)
            {
                jitteredSliceDepths[j][i] = sliceDepths[i] * (1 + offset * sliceProportion);
            }
        }
    }

    private void calculateFrustumRays()
    {
        Vector3[] frustumRaysTmp = new Vector3[4];
        frustumRays = new Vector4[4];
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

    private void getPointLightData()
    {
        Light[] lights = FindObjectsOfType(typeof(Light)) as Light[];
        pointLights = new FogPointLight[lights.Length];
        pointLightCount = 0; ;
        for (int i = 0, j = 0; i < lights.Length; i++)
        {
            if (lights[i].type != LightType.Point)
            {
                continue;
            }
            pointLights[j].position = lights[i].transform.position;
            pointLights[j].color = lights[i].color;
            pointLights[j].range = lights[i].range;
            pointLights[j].intensity = lights[i].intensity;
            j = ++pointLightCount;
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
        if (fogVolume0 == null)
        {
            fogVolume0 = new RenderTexture(160, 90, 0, RenderTextureFormat.ARGBHalf);
            fogVolume1 = new RenderTexture(160, 90, 0, RenderTextureFormat.ARGBHalf);
            fogVolume2 = new RenderTexture(160, 90, 0, RenderTextureFormat.ARGBHalf);
            fogVolume0.enableRandomWrite = true;
            fogVolume1.enableRandomWrite = true;
            fogVolume2.enableRandomWrite = true;
            fogVolume0.volumeDepth = depthSliceCount;
            fogVolume1.volumeDepth = depthSliceCount;
            fogVolume2.volumeDepth = depthSliceCount;
            fogVolume0.filterMode = FilterMode.Bilinear;
            fogVolume1.filterMode = FilterMode.Bilinear;
            fogVolume2.filterMode = FilterMode.Bilinear;
            fogVolume0.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            fogVolume1.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            fogVolume2.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            fogVolume0.Create();
            fogVolume1.Create();
            fogVolume2.Create();

            currentfogVolume = fogVolume0;
            historyFogVolume = fogVolume1;
            blendedFogVolume = fogVolume2;
        }
        if (accumulatedFogVolume == null)
        {
            accumulatedFogVolume = new RenderTexture(160, 90, 0, RenderTextureFormat.ARGBHalf);
            accumulatedFogVolume.enableRandomWrite = true;
            accumulatedFogVolume.volumeDepth = depthSliceCount;
            accumulatedFogVolume.filterMode = FilterMode.Bilinear;
            accumulatedFogVolume.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            accumulatedFogVolume.Create();
        }

        // compute all required variables
        Matrix4x4 viewProjectionMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;;
        calculateFrustumRays();
        float logfarOverNearInv = 1 / Mathf.Log(cam.farClipPlane / cam.nearClipPlane);

        //TODO: use command buffers instead of OnRenderImage (performance??)
        //TODO: fix depth texture delay in forward
        //TODO: temporal supersampling

        //TODO: shadow filtering: convert to exponential shadow maps
        //DONE: logarithmic depth slice distribution (right now its linear)
        // -> see formula one slide 5 http://advances.realtimerendering.com/s2016/Siggraph2016_idTech6.pdf
        //TODO: release buffers correctly (might be the reason for some crashes)


        directionalLightData.setComputeBuffer(densityLightingKernel, densityLightingShader);
        pointLightBuffer.SetData(pointLights);

        densityLightingShader.SetBuffer(densityLightingKernel, "pointLights", pointLightBuffer);
        densityLightingShader.SetVector("cameraPosition", transform.position);
        densityLightingShader.SetTextureFromGlobal(densityLightingKernel, "cascadeShadowMap", "_CascadeShadowMapCopy");
        densityLightingShader.SetTexture(densityLightingKernel, "fogVolume", currentfogVolume);
        densityLightingShader.SetVectorArray("frustumRays", frustumRays);
        densityLightingShader.SetFloat("nearPlane", cam.nearClipPlane);
        densityLightingShader.SetFloat("farPlane", cam.farClipPlane);
        densityLightingShader.SetFloat("scattering", scattering);
        densityLightingShader.SetFloat("g", anisotropy);
        densityLightingShader.SetFloat("fogHeight", fogHeight);
        densityLightingShader.SetFloat("fogFalloff", fogFalloff);
        densityLightingShader.SetFloat("transmittance", 1 - transmittance);
        densityLightingShader.SetFloats("sliceDepths", jitteredSliceDepths[jitterIndex]);
        densityLightingShader.SetFloat("logfarOverNearInv", logfarOverNearInv);
        densityLightingShader.SetVector("scatterColor", scatterColor);
        densityLightingShader.SetFloat("time", Time.time);
        densityLightingShader.SetFloat("noiseIntensity", noiseIntensity);
        densityLightingShader.SetBuffer(densityLightingKernel, "pointLights", pointLightBuffer);
        densityLightingShader.SetInt("pointLightCount", pointLightCount);
        densityLightingShader.SetVector("ambientLightColor", ambientColor);
        densityLightingShader.SetFloat("noiseSize", noiseSize);
        densityLightingShader.SetVector("noiseDirection", noiseDirection);
        densityLightingShader.Dispatch(densityLightingKernel, 40, 24, depthSliceCount / 4);
        densityLightingShader.SetMatrix("historyViewProjection", viewProjectionMatrix);

        tssBlendShader.SetVector("cameraPosition", transform.position);
        tssBlendShader.SetTexture(tssBlendKernel, "fogVolume", currentfogVolume);
        tssBlendShader.SetTexture(tssBlendKernel, "historyFogVolume", historyFogVolume);
        tssBlendShader.SetTexture(tssBlendKernel, "blendedFogVolume", blendedFogVolume);
        tssBlendShader.SetVectorArray("frustumRays", frustumRays);
        tssBlendShader.SetFloat("nearPlane", cam.nearClipPlane);
        tssBlendShader.SetFloat("farPlane", cam.farClipPlane);
        tssBlendShader.SetFloats("sliceDepths", jitteredSliceDepths[jitterIndex]);
        tssBlendShader.SetFloat("logfarOverNearInv", logfarOverNearInv);
        tssBlendShader.SetFloat("time", Time.time);
        tssBlendShader.Dispatch(tssBlendKernel, 40, 24, depthSliceCount / 4);
        tssBlendShader.SetMatrix("historyViewProjection", viewProjectionMatrix);

        scatteringShader.SetTexture(scatteringKernel, "accumulatedFogVolume", accumulatedFogVolume);
        scatteringShader.SetTexture(scatteringKernel, "fogVolume", blendedFogVolume);
        scatteringShader.SetFloats("sliceDepths", sliceDepths);
        scatteringShader.SetVector("scatterColor", scatterColor);
        scatteringShader.Dispatch(scatteringKernel, 20, 12, 1);

        applyFogShader.SetVector("resolution", resolution);
        applyFogShader.SetTextureFromGlobal(applyFogKernel, "depth", "_CameraDepthTexture");
        applyFogShader.SetTexture(applyFogKernel, "source", source);
        applyFogShader.SetTexture(applyFogKernel, "result", tempDestination);
        applyFogShader.SetTexture(applyFogKernel, "accumulatedFogVolume", accumulatedFogVolume);
        applyFogShader.SetFloat("nearPlane", cam.nearClipPlane);
        applyFogShader.SetFloat("farPlane", cam.farClipPlane);
        applyFogShader.SetFloat("logfarOverNearInv", logfarOverNearInv);
        applyFogShader.Dispatch(applyFogKernel, (tempDestination.width + 7) / 8, (tempDestination.height + 7) / 8, 1);

        //applyFogMaterial.SetTexture("mainTex", source);
        //applyFogMaterial.SetTexture("froxelVolume", fogVolume);
        //Graphics.Blit(tempDestination, destination, applyFogMaterial);
        Graphics.Blit(tempDestination, destination);
    }
}
