using UnityEngine.Rendering;
using UnityEngine;
using System;

[Serializable]
public struct LightControlPoint
{
    [Range(0, 380)]
    public float angle;
    public float intensity;
    public Color color;
    [ColorUsage(true, true)]
    public Color ambientColor;
}

[ExecuteAlways]
public class DirectionalLightData : MonoBehaviour
{
    [SerializeField]
    private LightControlPoint[] controlPoints;

    public Shader lightDataShader;
    public Shader esmShader;
    public ComputeShader esmCompute;
    public Material skyMaterial;

    private CommandBuffer commandBuffer0;
    private CommandBuffer commandBuffer1;
    private RenderTexture shadowMapCopy;

    // 4*4*4+2*4+4+4=80 (unity_WorldToShadow[4], lightSplits, lightColor, ambientColor)
    private ComputeBuffer lightDataBuffer;
    private Material lightDataCopyMaterial;
    private Material esmMaterial;

    private Light light;

    private void Start()
    {
        commandBuffer0 = new CommandBuffer();
        commandBuffer1 = new CommandBuffer();
        lightDataBuffer = new ComputeBuffer(1, 304);
        lightDataCopyMaterial = new Material(lightDataShader);
        //esmMaterial = new Material(esmShader);

        // copy cascade shadowmap
        RenderTargetIdentifier shadowmap = BuiltinRenderTextureType.CurrentActive;
        shadowMapCopy = new RenderTexture(4096, 4096, 0, RenderTextureFormat.RFloat);
        //shadowMapCopy = new RenderTexture(512, 512, 0, RenderTextureFormat.RFloat);
        shadowMapCopy.filterMode = FilterMode.Point;
        shadowMapCopy.wrapMode = TextureWrapMode.Clamp;
        //shadowMapCopy.enableRandomWrite = true;
        shadowMapCopy.useMipMap = true;
        shadowMapCopy.autoGenerateMips = false;
        commandBuffer0.SetShadowSamplingMode(shadowmap, ShadowSamplingMode.RawDepth);
        var id = new RenderTargetIdentifier(shadowMapCopy);

        // bug in CommandBuffer.Blit
        // https://forum.unity.com/threads/commandbuffer-blit-with-no-custom-shader-commandbuffer-blit-with-internal_blitcopy-shader.432699/
        Shader shader = Shader.Find("Hidden/ESM");
        Material material = new Material(shader);
        commandBuffer0.SetGlobalTexture("_MainTex", shadowmap);
       // commandBuffer0.Blit(shadowmap, shadowMapCopy, material, 0);
        //commandBuffer0.GenerateMips(shadowMapCopy);
        //commandBuffer0.SetComputeTextureParam(esmCompute, 0, "shadowMap", shadowmap);
        //commandBuffer0.SetComputeTextureParam(esmCompute, 0, "expShadowMap", id);
        //commandBuffer0.DispatchCompute(esmCompute, 0, 64, 64, 1);
        //commandBuffer0.Blit(shadowmap, id);
        commandBuffer0.SetGlobalTexture("_CascadeShadowMapCopy", shadowmap);

        //copy light data
        Graphics.SetRandomWriteTarget(1, lightDataBuffer);
        commandBuffer1.DrawProcedural(Matrix4x4.identity, lightDataCopyMaterial, 0, MeshTopology.Points, 1);

        light = GetComponent<Light>();
        light.AddCommandBuffer(LightEvent.AfterShadowMap, commandBuffer0);
        light.AddCommandBuffer(LightEvent.BeforeScreenspaceMask, commandBuffer1);
    }


    private void Update()
    {
        //float rot = light.transform.rotation.eulerAngles.x;
        //LightControlPoint lpc0 = controlPoints[controlPoints.Length - 2];
        //LightControlPoint lpc1 = controlPoints[controlPoints.Length - 1];
        //float lpc0Angle = lpc0.angle - 360;
        //if (lpc0Angle - 360 <= rot && rot < lpc1.angle)
        //{
        //    float lerpFactor = (rot - lpc0Angle) / (lpc1.angle - lpc0Angle);
        //    light.color = Color.Lerp(lpc0.color, lpc1.color, Mathf.SmoothStep(0f, 1f, lerpFactor));
        //    light.intensity = Mathf.SmoothStep(lpc0.intensity, lpc1.intensity, lerpFactor);
        //    RenderSettings.ambientLight = Color.Lerp(lpc0.ambientColor, lpc1.ambientColor, Mathf.SmoothStep(0f, 1f, lerpFactor));
        //}
        //for (int i = 1; i < controlPoints.Length; i++)
        //{            
        //    lpc0 = controlPoints[i - 1];
        //    lpc1 = controlPoints[i];
        //    float angle0 = lpc0.angle;
        //    float angle1 = lpc1.angle;
        //    if (angle0 <= rot && rot <= angle1)
        //    {
        //        float lerpFactor = (rot - angle0) / (angle1 - angle0);
        //        light.color = Color.Lerp(lpc0.color, lpc1.color, Mathf.SmoothStep(0f, 1f, lerpFactor));
        //        light.intensity = Mathf.Lerp(lpc0.intensity, lpc1.intensity, lerpFactor);
        //        RenderSettings.ambientLight = Color.Lerp(lpc0.ambientColor, lpc1.ambientColor, Mathf.SmoothStep(0f, 1f, lerpFactor));
        //    }
        //}
        

       // transform.Rotate(Time.deltaTime * (0.0f + Input.GetAxis("Jump") * 15), 0, 0, Space.Self);


        skyMaterial.SetMatrix("_rotationMatrix", Matrix4x4.Rotate(transform.rotation));
    }

    public void updateData()
    {
        if (commandBuffer0 != null)
        {
           // commandBuffer0.Clear();
        }
        if (commandBuffer1 != null)
        {
            //commandBuffer1.Clear();
        }
        
    }

    public void setComputeBuffer(int kernelIndex, ComputeShader shader)
    {
        shader.SetBuffer(kernelIndex, "lightData", lightDataBuffer);
    }

    private void OnApplicationQuit()
    {
        lightDataBuffer.Release();
        commandBuffer0.Release();
        commandBuffer1.Release();
    }
}
