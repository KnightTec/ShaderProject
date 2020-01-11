using UnityEngine;
using UnityEngine.Rendering;

//https://bartwronski.files.wordpress.com/2014/08/bwronski_volumetric_fog_siggraph2014.pdf

[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public class VolumetricFogRenderer : MonoBehaviour
{
    public Vector3Int volumeResolution = new Vector3Int(320, 180, 128);
    [Range(0, 10)]
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
    public ComputeShader temporalFilterShader;
    public ComputeShader scatteringShader;
    public ComputeShader applyFogShader;
    public Shader applyFogShader0;
    public float jitterStrength = 1;
    public Color ambientColor;

    private RenderTexture tempDestination;

    private int densityLightingKernel;
    private int temporalFilterKernel;
    private int scatteringKernel;
    private int applyFogKernel;
    private Material applyFogMaterial;

    private Camera cam;
    private Vector4 resolution;

    private RenderTexture fogVolume0;
    private RenderTexture fogVolume4;
    private RenderTexture fogVolume5;
    private RenderTexture currentfogVolume;
    private RenderTexture exponentialHistoryFogVolume;
    private RenderTexture filteredFogVolume;

    private ComputeBuffer pointLightBuffer;

    private RenderTexture accumulatedFogVolume;
    private Vector4[] frustumRays;
    private float[] sliceDepths;
    private float[][] jitteredSliceDepths;
    private int jitterIndex = 0;
    private Matrix4x4 historyViewProj_1;
    private int swapCounter = 0;
    private int[] bayerMatrix = {
        0, 8, 2, 10,
        12, 4, 14, 6,
        3, 11, 1, 9,
        15, 7, 13, 5
    };
    private Vector4[][] jitteredFrustumRays;
    private float[] depthJitter;
   
    struct FogPointLight
    {
        public Vector4 position;
        public Vector4 color;
        public float range;
        public float intensity;
    };
    private FogPointLight[] pointLights;
    private int pointLightCount;

    private Vector3 volRes;
    private Vector4 clipPlanes;

    private CommandBuffer commandBuffer;

    void OnEnable()
    {
        GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth;
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        applyFogMaterial = new Material(applyFogShader0);
        densityLightingKernel = densityLightingShader.FindKernel("CSMain");
        temporalFilterKernel = temporalFilterShader.FindKernel("CSMain");
        scatteringKernel = scatteringShader.FindKernel("CSMain");
        applyFogKernel = applyFogShader.FindKernel("CSMain");
        resolution = new Vector4();
        frustumRays = new Vector4[4];
        sliceDepths = new float[volumeResolution.z];
        jitteredSliceDepths = new float[15][];
        calculateSliceDepths();
        pointLightBuffer = new ComputeBuffer(64, 40);
        float logfarOverNearInv = 1 / Mathf.Log(cam.farClipPlane / cam.nearClipPlane);
        float logNearPlane = Mathf.Log(cam.nearClipPlane);
        clipPlanes = new Vector4(cam.nearClipPlane, cam.farClipPlane, logfarOverNearInv, logNearPlane);
        volRes = new Vector3(volumeResolution.x, volumeResolution.y, volumeResolution.z);
    }

    private void initCommandBuffer()
    {
        commandBuffer = new CommandBuffer();
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
        if (swapCounter % 2 == 0)
        {
            exponentialHistoryFogVolume = fogVolume4;
            filteredFogVolume = fogVolume5;
        }
        else
        {
            exponentialHistoryFogVolume = fogVolume5;
            filteredFogVolume = fogVolume4;
        }
        swapCounter = (swapCounter + 1) % 2;
        jitterIndex = (jitterIndex + 1) % 15;
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
        // logarithmic depth distribution (http://advances.realtimerendering.com/s2016/Siggraph2016_idTech6.pdf)
        for (int i = 0; i < volumeResolution.z; i++)
        {
            sliceDepths[i] = cam.nearClipPlane * Mathf.Pow(farOverNear, i / (float)volumeResolution.z);
        }
        float sliceProportion = Mathf.Pow(farOverNear, 1 / (float)volumeResolution.z) - 1;
        depthJitter = new float[15];
        for (int j = 0; j < 15; j++)
        {
            jitteredSliceDepths[j] = new float[volumeResolution.z];
            float offset = haltonSequence(j + 1, 2) - 0.5f;
            offset *= jitterStrength;
            for (int i = 0; i < volumeResolution.z; i++)
            {
                jitteredSliceDepths[j][i] = sliceDepths[i] * (1 + offset * sliceProportion);
            }
            depthJitter[j] = 1 + offset * sliceProportion;
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
        Vector4 rayRightOffset = (frustumRays[3] - frustumRays[0]) / volumeResolution.x;
        Vector4 rayUpOffset = (frustumRays[1] - frustumRays[0]) / volumeResolution.y;
        jitteredFrustumRays = new Vector4[15][];
        for (int i = 0; i < 15; i++)
        {
            float offsetX = haltonSequence(i + 1, 3) - 0.5f;
            float offsetY = haltonSequence(i + 1, 5) - 0.5f;
            jitteredFrustumRays[i] = new Vector4[4];
            for (int j = 0; j < 4; j++)
            {
                jitteredFrustumRays[i][j] = frustumRays[j] + offsetX * rayRightOffset + offsetY * rayUpOffset;
            }
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

    private void createFogVolume(ref RenderTexture volume)
    {
        if (volume != null)
        {
            volume.Release();
        }
        volume = new RenderTexture(volumeResolution.x, volumeResolution.y, 0, RenderTextureFormat.ARGBHalf);
        volume.enableRandomWrite = true;
        volume.volumeDepth = volumeResolution.z;
        volume.filterMode = FilterMode.Bilinear;
        volume.dimension = TextureDimension.Tex3D;
        volume.Create();
    }

    [ImageEffectOpaque]
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // recreate texture if size was changed
        if (null == tempDestination || source.width != tempDestination.width
           || source.height != tempDestination.height)
        {
            if (tempDestination != null)
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
            createFogVolume(ref fogVolume0);
            createFogVolume(ref fogVolume4);
            createFogVolume(ref fogVolume5);

            currentfogVolume = fogVolume0;
            exponentialHistoryFogVolume = fogVolume4;
            filteredFogVolume = fogVolume5;

            createFogVolume(ref accumulatedFogVolume);
        }

        // compute all required variables
        Matrix4x4 viewProjectionMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;;
        calculateFrustumRays();
        

        //TODO: use command buffers instead of OnRenderImage (performance??)
        //TODO: fix depth texture delay in forward
        //TODO: release buffers correctly (might be the reason for some crashes)

        directionalLightData.setComputeBuffer(densityLightingKernel, densityLightingShader);
        pointLightBuffer.SetData(pointLights);

        densityLightingShader.SetBuffer(densityLightingKernel, "pointLights", pointLightBuffer);
        densityLightingShader.SetVector("cameraPosition", transform.position);
        densityLightingShader.SetTextureFromGlobal(densityLightingKernel, "cascadeShadowMap", "_CascadeShadowMapCopy");
        densityLightingShader.SetTexture(densityLightingKernel, "fogVolume", currentfogVolume);
        densityLightingShader.SetVectorArray("frustumRays", frustumRays);
        densityLightingShader.SetFloat("scattering", scattering);
        densityLightingShader.SetFloat("g", anisotropy);
        densityLightingShader.SetFloat("fogHeight", fogHeight);
        densityLightingShader.SetFloat("fogFalloff", fogFalloff);
        densityLightingShader.SetFloat("transmittance", 1 - transmittance);
        densityLightingShader.SetFloats("sliceDepths", sliceDepths);
        densityLightingShader.SetInt("jitterIndex", jitterIndex);
        densityLightingShader.SetVector("scatterColor", scatterColor);
        densityLightingShader.SetFloat("time", Time.time);
        densityLightingShader.SetFloat("noiseIntensity", noiseIntensity);
        densityLightingShader.SetBuffer(densityLightingKernel, "pointLights", pointLightBuffer);
        densityLightingShader.SetInt("pointLightCount", pointLightCount);
        densityLightingShader.SetVector("ambientLightColor", ambientColor);
        densityLightingShader.SetFloat("noiseSize", 1.0f / noiseSize);
        densityLightingShader.SetVector("noiseDirection", noiseDirection);
        densityLightingShader.SetVector("volumeResolution", volRes);
        densityLightingShader.SetInts("bayerMatrix", bayerMatrix);
        densityLightingShader.SetFloats("depthJitter", depthJitter);
        densityLightingShader.Dispatch(densityLightingKernel, (volumeResolution.x + 3) / 4, (volumeResolution.y + 3) / 4, (volumeResolution.z + 3) / 4);
        
        temporalFilterShader.SetVector("cameraPosition", transform.position);
        temporalFilterShader.SetTexture(temporalFilterKernel, "exponentialHistory", exponentialHistoryFogVolume);
        temporalFilterShader.SetTexture(temporalFilterKernel, "blendedFogVolume", currentfogVolume);
        temporalFilterShader.SetTexture(temporalFilterKernel, "result", filteredFogVolume);
        temporalFilterShader.SetVectorArray("frustumRays", frustumRays);
        temporalFilterShader.SetFloats("sliceDepths", sliceDepths);
        temporalFilterShader.SetVector("clipPlanes", clipPlanes);
        temporalFilterShader.SetVector("volumeResolution", volRes);
        temporalFilterShader.Dispatch(temporalFilterKernel, (volumeResolution.x + 3) / 4, (volumeResolution.y + 3) / 4, (volumeResolution.z + 3) / 4);
        temporalFilterShader.SetMatrix("historyViewProjection", viewProjectionMatrix);
        
        scatteringShader.SetTexture(scatteringKernel, "accumulatedFogVolume", accumulatedFogVolume);
        scatteringShader.SetTexture(scatteringKernel, "fogVolume", filteredFogVolume);
        scatteringShader.SetFloats("sliceDepths", sliceDepths);
        scatteringShader.SetVector("scatterColor", scatterColor);
        scatteringShader.Dispatch(scatteringKernel, (volumeResolution.x + 7) / 8, (volumeResolution.y + 7) / 8, 1);

        applyFogShader.SetVector("resolution", resolution);
        applyFogShader.SetTextureFromGlobal(applyFogKernel, "depth", "_CameraDepthTexture");
        applyFogShader.SetTexture(applyFogKernel, "source", source);
        applyFogShader.SetTexture(applyFogKernel, "result", tempDestination);
        applyFogShader.SetTexture(applyFogKernel, "accumulatedFogVolume", accumulatedFogVolume);
        applyFogShader.SetVector("volumeResolution", volRes);
        applyFogShader.SetVector("clipPlanes", clipPlanes);
        applyFogShader.Dispatch(applyFogKernel, (tempDestination.width + 7) / 8, (tempDestination.height + 7) / 8, 1);

        //applyFogMaterial.SetTexture("mainTex", source);
        //applyFogMaterial.SetTexture("froxelVolume", fogVolume);
        //Graphics.Blit(tempDestination, destination, applyFogMaterial);
        Graphics.Blit(tempDestination, destination);
    }
}
