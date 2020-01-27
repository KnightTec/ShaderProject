using UnityEngine;
using UnityEngine.Rendering;

// References:
// https://bartwronski.files.wordpress.com/2014/08/bwronski_volumetric_fog_siggraph2014.pdf
// https://www.ea.com/frostbite/news/physically-based-unified-volumetric-rendering-in-frostbite
// Volumetric Light Effects in Killzone: Shadow Fall (GPU Pro 6)
// Creating the Atmospheric World of Red Dead Redemption 2: A Complete and Integrated Solution (http://advances.realtimerendering.com/s2019/index.htm)

// Renders volumetric fog and atmosphere
[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public class VolumetricFogRenderer : MonoBehaviour
{
    [Header("Atmosphere")]
    [Range(0, 10)]
    public float atmosphereScattering = 0.01f;
    public Color atmosphereScatterColor = new Color(63, 127, 255);
    public float atmosphereHeight = 0;
    [Range(0.00001f, 0.2f)]
    public float atmosphereFalloff = 0.001f;
    [Range(1, 100)]
    public float sunLightMultiplier = 30;

    [Header("Fog")]
    public float distance = 150; // should not be higher than shadow distance
    public Vector3Int volumeResolution = new Vector3Int(240, 135, 128);
    // Note: 
    public bool selfShadow = false;
    [Range(0, 10)]
    public float scattering = 0.05f;
    public Color scatterColor = Color.white;
    [Range(-0.9f, 0.9f)]
    public float anisotropy = 0.6f;
    public float fogHeight;
    [Range(0.00001f, 0.5f)]
    public float fogFalloff = 0.1f;
    [Range(0, 1)]
    public float ambientIntensity = 0.5f;
    [Range(0, 1)]
    public float noiseIntensity = 0;
    [Range(0.1f, 100)]
    public float noiseSize = 8;
    public Vector3 noiseDirection;

    public float jitterStrength = 0.5f;

    private ComputeShader densityLightingShader;
    private ComputeShader temporalFilterShader;
    private ComputeShader scatteringShader;
    
    private Texture blueNoise1D;
    private Texture blueNoise4D;
    private int ditherIndex = 0;

    private int fogDensityLightingKernel;
    private int atmoDensityLightingKernel;
    private int temporalFilterKernel;
    private int fogScatteringKernel;
    private int atmoScatteringKernel;
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
    private ComputeBuffer spotLightBuffer;
    private ComputeBuffer worldToShadowBuffer;

    private CommandBuffer cbGrabCascadeShadowMap;
    private CommandBuffer cbGrabWorldToShadow;

    private Material lightDataCopyMaterial;

    private Vector4[] frustumRays;
    private float[] sliceDepths;
    private float[][] jitteredSliceDepths;
    private int jitterIndex = 0;
    private Matrix4x4 historyViewProj_1;
    private int swapCounter = 0;
    private Vector4[][] jitteredFrustumRays;
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
    private Vector4 clipParams;
    private Matrix4x4 projectiomMatrix;

    private Material skyMaterial;

    void OnEnable()
    {
        GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth;
    }

    void Start()
    {
        densityLightingShader = Resources.Load<ComputeShader>("Shaders/VolumetricFog/DensityLighting");
        temporalFilterShader = Resources.Load<ComputeShader>("Shaders/VolumetricFog/TemporalFilter");
        scatteringShader = Resources.Load<ComputeShader>("Shaders/VolumetricFog/Scattering");

        cam = GetComponent<Camera>();
        applyFogMaterial = new Material(Shader.Find("Hidden/ApplyFogPS"));
        fogDensityLightingKernel = densityLightingShader.FindKernel("CSMain");
        atmoDensityLightingKernel = 1;
        temporalFilterKernel = temporalFilterShader.FindKernel("CSMain");
        fogScatteringKernel = scatteringShader.FindKernel("CSMain");
        atmoScatteringKernel = 1;
        resolution = new Vector4();
        frustumRays = new Vector4[4];
        sliceDepths = new float[volumeResolution.z];
        atmoSliceDepths = new float[32];
        jitteredSliceDepths = new float[15][];
        calculateSliceDepths();

        pointLightBuffer = new ComputeBuffer(64, 40);
        spotLightBuffer = new ComputeBuffer(64, 64);
        pointLights = new FogPointLight[64];

        float logfarOverNearInv = 1 / Mathf.Log(distance / cam.nearClipPlane);
        float logNearPlane = Mathf.Log(cam.nearClipPlane);
        clipParams = new Vector4(cam.nearClipPlane, distance, logfarOverNearInv, logNearPlane);
        volRes = new Vector3(volumeResolution.x, volumeResolution.y, volumeResolution.z);
        float farPlane = cam.farClipPlane;
        cam.farClipPlane = distance;
        projectiomMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
        cam.farClipPlane = farPlane;

        blueNoise1D = Resources.Load<Texture>("Textures/BlueNoiseR");
        blueNoise4D = Resources.Load<Texture>("Textures/BlueNoiseRGBA");

        cbGrabCascadeShadowMap = new CommandBuffer();
        cbGrabWorldToShadow = new CommandBuffer();
        worldToShadowBuffer = new ComputeBuffer(1, 256);
        lightDataCopyMaterial = new Material(Shader.Find("Hidden/CopyWorldToShadowMatrix"));
        Graphics.SetRandomWriteTarget(1, worldToShadowBuffer);

        skyMaterial = Resources.Load<Material>("Materials/Skybox");
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

        ditherIndex = (ditherIndex + 1) % 4;

        skyMaterial.SetMatrix("_rotationMatrix", Matrix4x4.Rotate(directionalLight.transform.rotation));

        if (selfShadow)
        {
            fogDensityLightingKernel = 0;
        } 
        else
        {
            fogDensityLightingKernel = 2;
        }
    }

    private void OnPreRender()
    {
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
        for (int j = 0; j < 15; j++)
        {
            jitteredSliceDepths[j] = new float[volumeResolution.z];
            float offset = haltonSequence(j + 1, 2) - 0.5f;
            offset *= jitterStrength;
            for (int i = 0; i < volumeResolution.z; i++)
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
        pointLightCount = 0;
        for (int i = 0, j = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional)
            {
                if (lights[i] != directionalLight)
                {
                    directionalLight = lights[i];
                    cbGrabCascadeShadowMap.Clear();
                    // copy cascade shadowmap and worldToShadow matrices
                    RenderTargetIdentifier shadowmap = BuiltinRenderTextureType.CurrentActive;
                    cbGrabCascadeShadowMap.SetGlobalTexture("_CascadeShadowMapCopy", shadowmap);
                    
                    directionalLight.AddCommandBuffer(LightEvent.AfterShadowMap, cbGrabCascadeShadowMap);

                    cbGrabWorldToShadow.DrawProcedural(Matrix4x4.identity, lightDataCopyMaterial, 0, MeshTopology.Points, 1);
                    directionalLight.AddCommandBuffer(LightEvent.BeforeScreenspaceMask, cbGrabWorldToShadow);
                }
            } 
            else if (lights[i].type == LightType.Point)
            {
                pointLights[j].position = lights[i].transform.position;
                pointLights[j].color = lights[i].color;
                pointLights[j].range = lights[i].range;
                pointLights[j].intensity = lights[i].intensity;
                j = ++pointLightCount;
            }
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
        Vector3 volResInv = new Vector3(1.0f / volRes.x, 1.0f / volRes.y, 1.0f / volRes.z);

        //TODO: release buffers correctly (might be the reason for some crashes)

        pointLightBuffer.SetData(pointLights);

        // render volumetric fog
        densityLightingShader.SetBuffer(fogDensityLightingKernel, "lightData", worldToShadowBuffer);
        densityLightingShader.SetVector("lightSplitsNear", Shader.GetGlobalVector("_LightSplitsNear"));
        densityLightingShader.SetVector("lightSplitsFar", Shader.GetGlobalVector("_LightSplitsFar"));
        densityLightingShader.SetVector("dirLightColor", directionalLight.color * directionalLight.intensity);
        densityLightingShader.SetVector("dirLightDirection", dirLightDirection);
        densityLightingShader.SetBuffer(fogDensityLightingKernel, "pointLights", pointLightBuffer);
        densityLightingShader.SetInt("pointLightCount", pointLightCount);
        densityLightingShader.SetBuffer(fogDensityLightingKernel, "spotLights", spotLightBuffer);
        densityLightingShader.SetVector("cameraPosition", transform.position);
        densityLightingShader.SetTextureFromGlobal(fogDensityLightingKernel, "cascadeShadowMap", "_CascadeShadowMapCopy");
        densityLightingShader.SetTexture(fogDensityLightingKernel, "fogVolume", currentfogVolume);
        densityLightingShader.SetVectorArray("frustumRays", jitteredFrustumRays[jitterIndex]);
        densityLightingShader.SetFloat("scattering", scattering);
        densityLightingShader.SetFloat("g", anisotropy);
        densityLightingShader.SetFloat("k", -1.55f * anisotropy + 0.55f * Mathf.Pow(anisotropy, 3));
        densityLightingShader.SetFloat("fogHeight", fogHeight);
        densityLightingShader.SetFloat("fogFalloff", fogFalloff);
        densityLightingShader.SetFloats("sliceDepths", jitteredSliceDepths[jitterIndex]);
        densityLightingShader.SetVector("scatterColor", scatterColor);
        densityLightingShader.SetFloat("time", Time.time);
        densityLightingShader.SetFloat("noiseIntensity", noiseIntensity);
        densityLightingShader.SetVector("ambientLightColor", RenderSettings.ambientLight * ambientIntensity);
        densityLightingShader.SetFloat("noiseSize", 1.0f / noiseSize);
        densityLightingShader.SetVector("noiseDirection", noiseDirection);
        densityLightingShader.SetVector("volumeResolution", volResInv);
        densityLightingShader.SetTexture(fogDensityLightingKernel, "blueNoise", blueNoise1D);
        densityLightingShader.SetFloat("sliceProportion", sliceProportion);
        densityLightingShader.Dispatch(fogDensityLightingKernel, (volumeResolution.x + 7) / 8, (volumeResolution.y + 7) / 8, (volumeResolution.z));
        
        temporalFilterShader.SetVector("cameraPosition", transform.position);
        temporalFilterShader.SetTexture(temporalFilterKernel, "exponentialHistory", exponentialHistoryFogVolume);
        temporalFilterShader.SetTexture(temporalFilterKernel, "fogVolume", currentfogVolume);
        temporalFilterShader.SetTexture(temporalFilterKernel, "result", filteredFogVolume);
        temporalFilterShader.SetVectorArray("frustumRays", frustumRays);
        temporalFilterShader.SetFloats("sliceDepths", sliceDepths);
        temporalFilterShader.SetVector("clipParams", clipParams);
        temporalFilterShader.SetVector("volumeResolutionInv", volResInv);
        temporalFilterShader.SetVector("volumeRes", volRes);
        temporalFilterShader.SetFloat("farPlane", cam.farClipPlane);
        temporalFilterShader.SetFloat("distance", distance);
        temporalFilterShader.Dispatch(temporalFilterKernel, (volumeResolution.x + 7) / 8, (volumeResolution.y + 7) / 8, (volumeResolution.z + 7) / 8);
        temporalFilterShader.SetMatrix("historyViewProjection", viewProjectionMatrix);
        
        scatteringShader.SetTexture(fogScatteringKernel, "accumulatedFogVolume", accumulatedFogVolume);
        scatteringShader.SetTexture(fogScatteringKernel, "fogVolume", filteredFogVolume);
        scatteringShader.SetFloats("sliceDepths", sliceDepths);
        scatteringShader.SetVector("scatterColor", scatterColor);
        scatteringShader.SetInt("depthSliceCount", volumeResolution.z);
        scatteringShader.Dispatch(fogScatteringKernel, (volumeResolution.x + 7) / 8, (volumeResolution.y + 7) / 8, 1);

        // render atmosphere
        densityLightingShader.SetTexture(atmoDensityLightingKernel, "fogVolume", atmosphereVolume);
        densityLightingShader.SetVector("dirLightColor", Color.white * directionalLight.intensity);
        densityLightingShader.SetVectorArray("frustumRays", frustumRays);
        densityLightingShader.SetFloat("scattering", atmosphereScattering);
        densityLightingShader.SetFloat("fogHeight", atmosphereHeight);
        densityLightingShader.SetFloat("fogFalloff", atmosphereFalloff);
        densityLightingShader.SetFloats("sliceDepths", atmoSliceDepths);
        densityLightingShader.SetVector("scatterColor", atmosphereScatterColor);
        densityLightingShader.SetVector("ambientLightColor", Color.black);
        densityLightingShader.SetVector("volumeResolution", new Vector3(1, 1, 1) * 1.0f / 32.0f);
        densityLightingShader.SetFloat("sunLightIntensityMultiplier", sunLightMultiplier);
        densityLightingShader.Dispatch(atmoScatteringKernel, 4, 4, 32);

        scatteringShader.SetTexture(atmoScatteringKernel, "accumulatedFogVolume", accumulatedAtmoVol);
        scatteringShader.SetTexture(atmoScatteringKernel, "fogVolume", atmosphereVolume);
        scatteringShader.SetFloats("sliceDepths", atmoSliceDepths);
        scatteringShader.SetVector("scatterColor", atmosphereScatterColor);
        scatteringShader.SetInt("depthSliceCount", 32);
        scatteringShader.Dispatch(atmoScatteringKernel, 4, 4, 1);

        // apply fog and atmosphere to scene
        applyFogMaterial.SetTexture("_MainTex", source);
        applyFogMaterial.SetTexture("fogVolume", accumulatedFogVolume);
        applyFogMaterial.SetTexture("atmoVolume", accumulatedAtmoVol);
        applyFogMaterial.SetVector("clipPlanes", clipParams);
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
        applyFogMaterial.SetInt("ditherIndex", 0);
        Shader.EnableKeyword("FOG_FALLBACK");

        Graphics.Blit(source, destination, applyFogMaterial);
    }
}
