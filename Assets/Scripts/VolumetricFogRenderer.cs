using UnityEngine;
using UnityEngine.Rendering;

//https://bartwronski.files.wordpress.com/2014/08/bwronski_volumetric_fog_siggraph2014.pdf

[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public class VolumetricFogRenderer : MonoBehaviour
{
    [Header("Atmosphere")]
    [Range(0, 10)]
    public float atmosphereScattering = 0.01f;
    public Color atmosphereScatterColor = Color.white;
    public float atmosphereHeight = 0;
    [Range(0.00001f, 0.2f)]
    public float atmosphereFalloff = 0.001f;
    [Range(1, 100)]
    public float sunLightMultiplier = 1;

    [Header("Fog")]
    public float distance = 200;
    public Vector3Int volumeResolution = new Vector3Int(240, 135, 128);
    [Range(0, 10)]
    public float scattering = 0.1f;
    public Color scatterColor = Color.white;
    [Range(-0.99f, 0.99f)]
    public float anisotropy = 0;
    [Range(0, 1)]
    public float transmittance = 0;
    public float fogHeight;
    [Range(0.00001f, 0.2f)]
    public float fogFalloff;
    [Range(0, 1)]
    float ambientIntensity = 0.5f;
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

    private Texture blueNoise1D;
    private Texture blueNoise4D;
    private int ditherIndex = 0;

    private int fogDensityLightingKernel;
    private int atmoDensityLightingKernel;
    private int temporalFilterKernel;
    private int fogScatteringKernel;
    private int atmoScatteringKernel;
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
    private RenderTexture accumulatedFogVolume;

    private RenderTexture atmosphereVolume;
    private RenderTexture accumulatedAtmoVol;

    private ComputeBuffer pointLightBuffer;
   
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
    private float[] atmoSliceDepths;
   
    struct FogPointLight
    {
        public Vector4 position;
        public Vector4 color;
        public float range;
        public float intensity;
    };
    private FogPointLight[] pointLights;
    private int pointLightCount;
    private Light directionalLight;

    private Vector3 volRes;
    private Vector4 clipPlanes;
    private Matrix4x4 projectiomMatrix;

    private CommandBuffer commandBuffer;

    void OnEnable()
    {
        GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth;
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        applyFogMaterial = new Material(applyFogShader0);
        fogDensityLightingKernel = densityLightingShader.FindKernel("CSMain");
        atmoDensityLightingKernel = 1;
        temporalFilterKernel = temporalFilterShader.FindKernel("CSMain");
        fogScatteringKernel = scatteringShader.FindKernel("CSMain");
        atmoScatteringKernel = 1;
        applyFogKernel = applyFogShader.FindKernel("CSMain");
        resolution = new Vector4();
        frustumRays = new Vector4[4];
        sliceDepths = new float[volumeResolution.z];
        atmoSliceDepths = new float[32];
        jitteredSliceDepths = new float[15][];
        calculateSliceDepths();
        pointLightBuffer = new ComputeBuffer(64, 40);
        float logfarOverNearInv = 1 / Mathf.Log(distance / cam.nearClipPlane);
        float logNearPlane = Mathf.Log(cam.nearClipPlane);
        clipPlanes = new Vector4(cam.nearClipPlane, distance, logfarOverNearInv, logNearPlane);
        volRes = new Vector3(volumeResolution.x, volumeResolution.y, volumeResolution.z);

        float farPlane = cam.farClipPlane;
        cam.farClipPlane = distance;
        projectiomMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
        cam.farClipPlane = farPlane;

        blueNoise1D = Resources.Load<Texture>("Textures/BlueNoiseR");
        blueNoise4D = Resources.Load<Texture>("Textures/BlueNoiseRGBA");
    }

    void OnDestroy()
    {
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

        if (swapCounter == 0)
        { 
            ditherIndex = (ditherIndex + 1) % 4;
        }

    }

    private void OnPreRender()
    {
        directionalLightData.updateData();
        getLightData();
        calculateFrustumRays();
        if (fogVolume0 == null)
        {
            createFogVolume(ref fogVolume0);
            createFogVolume(ref fogVolume4);
            createFogVolume(ref fogVolume5);

            currentfogVolume = fogVolume0;
            exponentialHistoryFogVolume = fogVolume4;
            filteredFogVolume = fogVolume5;

            createFogVolume(ref accumulatedFogVolume);

            createAtmosphereVolume(ref atmosphereVolume);
            createAtmosphereVolume(ref accumulatedAtmoVol);
        }
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
        for (int i = 0; i < 32; i++)
        {
            atmoSliceDepths[i] = cam.nearClipPlane * Mathf.Pow(farOverNear, i / 32.0f);
        }

        float distOverNear = distance / cam.nearClipPlane;
        // logarithmic depth distribution (http://advances.realtimerendering.com/s2016/Siggraph2016_idTech6.pdf)
        for (int i = 0; i < volumeResolution.z; i++)
        {
            sliceDepths[i] = cam.nearClipPlane * Mathf.Pow(distOverNear, i / (float)volumeResolution.z);
        }
        float sliceProportion = Mathf.Pow(distOverNear, 1 / (float)volumeResolution.z) - 1;
        densityLightingShader.SetFloat("sliceProportion", sliceProportion);
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
            offsetX *= jitterStrength;
            offsetY *= jitterStrength;
            jitteredFrustumRays[i] = new Vector4[4];
            for (int j = 0; j < 4; j++)
            {
                jitteredFrustumRays[i][j] = frustumRays[j] + offsetX * rayRightOffset + offsetY * rayUpOffset;
            }
        }
    }

    private void getLightData()
    {
        Light[] lights = FindObjectsOfType(typeof(Light)) as Light[];
        pointLights = new FogPointLight[lights.Length];
        pointLightCount = 0; ;
        for (int i = 0, j = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional)
            {
                directionalLight = lights[i];
            }
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
    private void createAtmosphereVolume(ref RenderTexture volume)
    {
        if (volume != null)
        {
            volume.Release();
        }
        volume = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGBHalf);
        volume.enableRandomWrite = true;
        volume.volumeDepth = 32;
        volume.filterMode = FilterMode.Bilinear;
        volume.dimension = TextureDimension.Tex3D;
        volume.Create();
    }

    [ImageEffectOpaque]
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        resolution.x = source.width;
        resolution.y = source.height;
        resolution.z = 1.0f / source.width;
        resolution.w = 1.0f / source.height;       

        // compute all required variables
        Matrix4x4 viewProjectionMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;;
        Vector3 dirLightDirection = (directionalLight.transform.rotation * Vector3.back).normalized;
        float sliceProportion = Mathf.Pow(distance / cam.nearClipPlane, 1 / (float)volumeResolution.z) - 1;

        //TODO: use command buffers instead of OnRenderImage (performance??)
        //TODO: fix depth texture delay in forward
        //TODO: release buffers correctly (might be the reason for some crashes)


        // render volumetric fog
        directionalLightData.setComputeBuffer(fogDensityLightingKernel, densityLightingShader);
        densityLightingShader.SetVector("dirLightColor", directionalLight.color * directionalLight.intensity);
        densityLightingShader.SetVector("dirLightDirection", dirLightDirection);
        pointLightBuffer.SetData(pointLights);
        densityLightingShader.SetBuffer(fogDensityLightingKernel, "pointLights", pointLightBuffer);
        densityLightingShader.SetVector("cameraPosition", transform.position);
        densityLightingShader.SetTextureFromGlobal(fogDensityLightingKernel, "cascadeShadowMap", "_CascadeShadowMapCopy");
        densityLightingShader.SetTexture(fogDensityLightingKernel, "fogVolume", currentfogVolume);
        densityLightingShader.SetVectorArray("frustumRays", jitteredFrustumRays[jitterIndex]);
        densityLightingShader.SetFloat("scattering", scattering);
        densityLightingShader.SetFloat("g", anisotropy);
        densityLightingShader.SetFloat("fogHeight", fogHeight);
        densityLightingShader.SetFloat("fogFalloff", fogFalloff);
        densityLightingShader.SetFloat("transmittance", 1 - transmittance);
        densityLightingShader.SetFloats("sliceDepths", jitteredSliceDepths[jitterIndex]);
        densityLightingShader.SetInt("jitterIndex", jitterIndex);
        densityLightingShader.SetVector("scatterColor", scatterColor);
        densityLightingShader.SetFloat("time", Time.time);
        densityLightingShader.SetFloat("noiseIntensity", noiseIntensity);
        densityLightingShader.SetBuffer(fogDensityLightingKernel, "pointLights", pointLightBuffer);
        densityLightingShader.SetInt("pointLightCount", pointLightCount);
        densityLightingShader.SetVector("ambientLightColor", RenderSettings.ambientLight * ambientIntensity);
        densityLightingShader.SetFloat("noiseSize", 1.0f / noiseSize);
        densityLightingShader.SetVector("noiseDirection", noiseDirection);
        densityLightingShader.SetVector("volumeResolution", volRes);
        densityLightingShader.SetInts("bayerMatrix", bayerMatrix);
        densityLightingShader.SetFloats("depthJitter", depthJitter);
        densityLightingShader.SetTexture(fogDensityLightingKernel, "blueNoise", blueNoise4D);
        densityLightingShader.SetFloat("sliceProportion", sliceProportion);
        densityLightingShader.SetInt("ditherIndex", ditherIndex);
        densityLightingShader.Dispatch(fogDensityLightingKernel, (volumeResolution.x + 3) / 4, (volumeResolution.y + 3) / 4, (volumeResolution.z + 3) / 4);
        
        temporalFilterShader.SetVector("cameraPosition", transform.position);
        temporalFilterShader.SetTexture(temporalFilterKernel, "exponentialHistory", exponentialHistoryFogVolume);
        temporalFilterShader.SetTexture(temporalFilterKernel, "blendedFogVolume", currentfogVolume);
        temporalFilterShader.SetTexture(temporalFilterKernel, "result", filteredFogVolume);
        temporalFilterShader.SetVectorArray("frustumRays", frustumRays);
        temporalFilterShader.SetFloats("sliceDepths", sliceDepths);
        temporalFilterShader.SetVector("clipPlanes", clipPlanes);
        temporalFilterShader.SetVector("volumeResolution", volRes);
        temporalFilterShader.SetFloat("farPlane", cam.farClipPlane);
        temporalFilterShader.SetFloat("distance", distance);
        temporalFilterShader.Dispatch(temporalFilterKernel, (volumeResolution.x + 3) / 4, (volumeResolution.y + 3) / 4, (volumeResolution.z + 3) / 4);
        temporalFilterShader.SetMatrix("historyViewProjection", viewProjectionMatrix);
        
        scatteringShader.SetTexture(fogScatteringKernel, "accumulatedFogVolume", accumulatedFogVolume);
        scatteringShader.SetTexture(fogScatteringKernel, "fogVolume", filteredFogVolume);
        scatteringShader.SetFloats("sliceDepths", sliceDepths);
        scatteringShader.SetVector("scatterColor", scatterColor);
        scatteringShader.Dispatch(fogScatteringKernel, (volumeResolution.x + 7) / 8, (volumeResolution.y + 7) / 8, 1);


        // render atmosphere
        //TODO: fix variables
        densityLightingShader.SetTexture(atmoDensityLightingKernel, "fogVolume", atmosphereVolume);
        densityLightingShader.SetVector("dirLightColor", Color.white * directionalLight.intensity);
        densityLightingShader.SetVectorArray("frustumRays", frustumRays);
        densityLightingShader.SetFloat("scattering", atmosphereScattering);
        densityLightingShader.SetFloat("fogHeight", atmosphereHeight);
        densityLightingShader.SetFloat("fogFalloff", atmosphereFalloff);
        densityLightingShader.SetFloat("transmittance", 1);
        densityLightingShader.SetFloats("sliceDepths", atmoSliceDepths);
        densityLightingShader.SetVector("scatterColor", atmosphereScatterColor);
        densityLightingShader.SetVector("ambientLightColor", Color.black);
        densityLightingShader.SetVector("volumeResolution", new Vector3(32, 32, 32));
        densityLightingShader.SetFloat("sunLightIntensityMultiplier", sunLightMultiplier);
        densityLightingShader.Dispatch(atmoScatteringKernel, 8, 8, 8);

        scatteringShader.SetTexture(atmoScatteringKernel, "accumulatedFogVolume", accumulatedAtmoVol);
        scatteringShader.SetTexture(atmoScatteringKernel, "fogVolume", atmosphereVolume);
        scatteringShader.SetFloats("sliceDepths", atmoSliceDepths);
        scatteringShader.SetVector("scatterColor", atmosphereScatterColor);
        scatteringShader.Dispatch(atmoScatteringKernel, 4, 4, 1);


        applyFogMaterial.SetTexture("_MainTex", source);
        applyFogMaterial.SetTexture("fogVolume", accumulatedFogVolume);
        applyFogMaterial.SetTexture("atmoVolume", accumulatedAtmoVol);
        applyFogMaterial.SetVector("clipPlanes", clipPlanes);
        applyFogMaterial.SetFloat("farPlane", cam.farClipPlane);
        applyFogMaterial.SetFloat("distance", distance);
        applyFogMaterial.SetMatrix("viewProjectionInv", viewProjectionMatrix.inverse);
        applyFogMaterial.SetFloat("fogHeight", fogHeight);
        applyFogMaterial.SetFloat("fogFalloff", fogFalloff);
        applyFogMaterial.SetVector("scatterColor", scatterColor);
        applyFogMaterial.SetFloat("scattering", scattering);
        applyFogMaterial.SetVector("ambientColor", RenderSettings.ambientLight * ambientIntensity);
        applyFogMaterial.SetVector("dirLightColor", directionalLight.color * directionalLight.intensity);
        applyFogMaterial.SetVector("dirLightDirection", dirLightDirection);
        applyFogMaterial.SetFloat("g", anisotropy);
        applyFogMaterial.SetFloat("noiseIntensity", noiseIntensity);
        applyFogMaterial.SetFloat("logfarOverNearInv", 1 / Mathf.Log(cam.farClipPlane / cam.nearClipPlane));
        applyFogMaterial.SetVector("volumeResolutionWH", new Vector4(volRes.x, volRes.y, 1.0f / volRes.x, 1.0f / volRes.y));
        applyFogMaterial.SetTexture("blueNoiseTex", blueNoise4D);
        Shader.EnableKeyword("FOG_FALLBACK");

        Graphics.Blit(source, destination, applyFogMaterial);
        //Graphics.Blit(tempDestination, destination);
    }
}
