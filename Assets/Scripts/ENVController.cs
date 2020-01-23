using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class ENVController : MonoBehaviour
{
    [Range(0, 1)]
    public float time = 0.3f;

    public Gradient sun;
    public GradientColorKey[] sunColorKey;
    public GradientAlphaKey[] sunAlphaKey;
    public float sunIntensityMultiplier = 1;

    public Gradient ambient;
    public GradientColorKey[] ambientColorKey;
    public GradientAlphaKey[] ambientAlphaKey;
    public float ambientIntensityMultiplier = 1;

    public AnimationCurve fogFalloff;
    public AnimationCurve fogScattering;
    public float fogScatteringMultiplier = 1;

    public Light directionalLight;
    public VolumetricFogRenderer fogRenderer;
    public Material grassMaterial;

    void Update()
    {
        if (directionalLight != null)
        {
            Color sample = sun.Evaluate(time);
            directionalLight.color = sample;
            directionalLight.intensity = sample.a * sunIntensityMultiplier + 0.001f;
            directionalLight.transform.localRotation = Quaternion.Euler((time * 360.0f) - 90.0f, 0, 0);
        }
        Color ambientSample = ambient.Evaluate(time);
        RenderSettings.ambientLight = ambientSample * ambientSample.a * ambientIntensityMultiplier;
        if (fogRenderer != null)
        {
            fogRenderer.fogFalloff = fogFalloff.Evaluate(time);
            fogRenderer.scattering = fogScattering.Evaluate(time) * fogScatteringMultiplier;
        }

        // Insert other controls here

        time += Time.deltaTime * Input.GetAxis("Jump") * 0.01f;
        if (time > 1)
        {
            time = 0;
        }
    }
}
