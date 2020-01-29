using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollisionDepthRenderer : MonoBehaviour
{
    public GameObject obj;
    public int textureSize;
    Material mat;
    public float depthLevel;
    Camera depthCamera;
    Material depthMat;
    RenderTexture tex;
    Texture texMat;
    int i = 0;
    
    public int timeOut;
    void Start()
    {
        i = timeOut;
        tex = new RenderTexture(textureSize,textureSize,24);

        mat = obj.GetComponent<MeshRenderer>().materials[0];
        mat.SetTexture("_CollisionTexture", 
            new Texture2D (textureSize,textureSize,TextureFormat.RGBA32,false));
        texMat = mat.GetTexture("_CollisionTexture");

        depthCamera = gameObject.GetComponent<Camera>();
        depthCamera.depthTextureMode = depthCamera.depthTextureMode |DepthTextureMode.Depth;
        depthCamera.aspect = 1;


        depthMat = new Material(
            Shader.Find("Custom/DepthShader")
        );
        depthMat.hideFlags = HideFlags.HideAndDontSave;
        
        depthCamera.targetTexture = tex;
    }

    private void OnDisable () {
        if ( depthMat != null )
            DestroyImmediate(depthMat);
    }

    private void OnRenderImage ( RenderTexture src, RenderTexture dest ) {
        if ( depthMat != null && (i ++ % timeOut == 0) ) {
            depthMat.SetFloat("_DepthLevel", depthLevel);
            mat.SetFloat("_CollisionFar", depthCamera.farClipPlane);
            Graphics.Blit (src,tex, depthMat);
            Graphics.CopyTexture(tex, texMat);
            i = 0;
        }
    }
}
