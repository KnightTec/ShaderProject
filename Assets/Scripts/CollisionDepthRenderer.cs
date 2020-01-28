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
    void Start()
    {
        tex = new RenderTexture(textureSize,textureSize,24);

        mat = obj.GetComponent<MeshRenderer>().materials[0];
        mat.SetTexture("_CollisionTexture", 
            new Texture2D (textureSize,textureSize,TextureFormat.RGBA32,false));
        texMat = mat.GetTexture("_CollisionTexture");

        Debug.Log(mat.name);
        Debug.Log(texMat.name);

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
        if ( depthMat != null ) {
            depthMat.SetFloat("_DepthLevel", depthLevel);
            Graphics.Blit (src,tex,depthMat);
            Graphics.CopyTexture(tex, texMat);
        }
    }
}
