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
    public Material skyMaterial;

    private CommandBuffer commandBuffer1;

    private ComputeBuffer lightDataBuffer;
    private Material lightDataCopyMaterial;

    private Light light;

    private void Start()
    { 
        commandBuffer1 = new CommandBuffer();
        lightDataBuffer = new ComputeBuffer(1, 256);
        lightDataCopyMaterial = new Material(lightDataShader);

        //copy light data
        Graphics.SetRandomWriteTarget(1, lightDataBuffer);
        commandBuffer1.DrawProcedural(Matrix4x4.identity, lightDataCopyMaterial, 0, MeshTopology.Points, 1);

        light = GetComponent<Light>();
        light.AddCommandBuffer(LightEvent.BeforeScreenspaceMask, commandBuffer1);
    }


    private void Update()
    {
        skyMaterial.SetMatrix("_rotationMatrix", Matrix4x4.Rotate(transform.rotation));
    }

    public void setComputeBuffer(int kernelIndex, ComputeShader shader)
    {
        shader.SetBuffer(kernelIndex, "lightData", lightDataBuffer);
    }

    private void OnApplicationQuit()
    {
        lightDataBuffer.Release();
        commandBuffer1.Release();
    }
}
