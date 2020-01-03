using UnityEngine.Rendering;
using UnityEngine;

[ExecuteAlways]
public class DirectionalLightData : MonoBehaviour
{
    public Shader lightDataShader;
    public Shader esmShader;

    private CommandBuffer commandBuffer0;
    private CommandBuffer commandBuffer1;
    private RenderTexture shadowMapCopy;

    // 4*4*4+2*4+4+4=80 (unity_WorldToShadow[4], lightSplits, lightColor, ambientColor)
    private ComputeBuffer lightDataBuffer;
    private Material lightDataCopyMaterial;
    private Material esmMaterial;

    private void Start()
    {
        commandBuffer0 = new CommandBuffer();
        commandBuffer1 = new CommandBuffer();
        lightDataBuffer = new ComputeBuffer(1, 352);
        lightDataCopyMaterial = new Material(lightDataShader);
        esmMaterial = new Material(esmShader);

        // copy cascade shadowmap
        RenderTargetIdentifier shadowmap = BuiltinRenderTextureType.CurrentActive;
        shadowMapCopy = new RenderTexture(1024, 1024, 0);
        shadowMapCopy.filterMode = FilterMode.Point;
        commandBuffer0.SetShadowSamplingMode(shadowmap, ShadowSamplingMode.RawDepth);
        var id = new RenderTargetIdentifier(shadowMapCopy);

        // bug in CommandBuffer.Blit
        // https://forum.unity.com/threads/commandbuffer-blit-with-no-custom-shader-commandbuffer-blit-with-internal_blitcopy-shader.432699/
        Shader shader = Shader.Find("Hidden/ESM");
        Material material = new Material(shader);
        commandBuffer0.SetGlobalTexture("_MainTex", shadowmap);
        commandBuffer0.Blit(shadowmap, shadowMapCopy);
        //commandBuffer0.Blit(shadowmap, id);
        commandBuffer0.SetGlobalTexture("_CascadeShadowMapCopy", id);

        //copy light data
        Graphics.SetRandomWriteTarget(1, lightDataBuffer);
        commandBuffer1.DrawProcedural(Matrix4x4.identity, lightDataCopyMaterial, 0, MeshTopology.Points, 1);

        Light light = GetComponent<Light>();
        light.AddCommandBuffer(LightEvent.AfterShadowMap, commandBuffer0);
        light.AddCommandBuffer(LightEvent.BeforeScreenspaceMask, commandBuffer1);
    }

    private void Update()
    {
        //transform.Rotate(0, Time.deltaTime * 10, 0, Space.World);
    }

    private void init()
    {
        if (commandBuffer0 != null)
        {
            return;
        }
        
    }

    public void updateData()
    {
        init();
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
