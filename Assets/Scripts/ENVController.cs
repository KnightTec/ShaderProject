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
    public float fogFalloffMultiplier = 1;
    public AnimationCurve fogScattering;
    public float fogScatteringMultiplier = 1;

    public Light directionalLight;
    public VolumetricFogRenderer fogRenderer;
    public Material grassMaterial;

    private float noiseIntensity;
    private float noiseSpeed;
    private bool taa;

    private void Start()
    {
        noiseIntensity = fogRenderer.noiseIntensity;
        noiseSpeed = fogRenderer.noiseDirection.x;
        taa = fogRenderer.taa;
    }

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
            fogRenderer.fogFalloff = fogFalloff.Evaluate(time) * fogFalloffMultiplier;
            fogRenderer.scattering = fogScattering.Evaluate(time) * fogScatteringMultiplier;
            fogRenderer.noiseIntensity = noiseIntensity;
            fogRenderer.noiseDirection.x = noiseSpeed;
            fogRenderer.taa = taa;
        }

        // Insert other controls here
        if (Input.GetKey(KeyCode.Keypad1))
        {
            if (Input.GetKey(KeyCode.KeypadPlus))
            {
                time += Time.deltaTime * 0.0015f;
            }
            else if (Input.GetKey(KeyCode.KeypadMinus))
            {
                time -= Time.deltaTime * 0.0015f;
            }
        }
        if (Input.GetKey(KeyCode.Keypad2))
        {
            if (Input.GetKey(KeyCode.KeypadPlus))
            {
                time += Time.deltaTime * 0.02f;
            }
            else if (Input.GetKey(KeyCode.KeypadMinus))
            {
                time -= Time.deltaTime * 0.02f;
            }
        }
        if (Input.GetKey(KeyCode.Keypad3))
        {
            if (Input.GetKey(KeyCode.KeypadPlus))
            {
                fogFalloffMultiplier += Time.deltaTime * 0.5f;
            }
            else if (Input.GetKey(KeyCode.KeypadMinus))
            {
                fogFalloffMultiplier -= Time.deltaTime * 0.5f;
            }
        }
        if (Input.GetKey(KeyCode.Keypad4))
        {
            if (Input.GetKey(KeyCode.KeypadPlus))
            {
                fogScatteringMultiplier += Time.deltaTime * 10f;
            }
            else if (Input.GetKey(KeyCode.KeypadMinus))
            {
                fogScatteringMultiplier -= Time.deltaTime * 10f;
            }
        }
        fogScatteringMultiplier = Mathf.Max(fogScatteringMultiplier, 0);
        fogFalloffMultiplier = Mathf.Max(fogFalloffMultiplier, 0);

        if (Input.GetKey(KeyCode.Keypad5))
        {
            if (Input.GetKey(KeyCode.KeypadPlus))
            {
                noiseIntensity += Time.deltaTime;
            }
            else if (Input.GetKey(KeyCode.KeypadMinus))
            {
                noiseIntensity -= Time.deltaTime;
            }
            noiseIntensity = Mathf.Clamp01(noiseIntensity);
        }
        if (Input.GetKey(KeyCode.Keypad6))
        {
            if (Input.GetKey(KeyCode.KeypadPlus))
            {
                noiseSpeed += Time.deltaTime * 10;
            }
            else if (Input.GetKey(KeyCode.KeypadMinus))
            {
                noiseSpeed -= Time.deltaTime * 10;
            }
            noiseIntensity = Mathf.Clamp01(noiseIntensity);
        }
        if (Input.GetKeyDown(KeyCode.Keypad7))
        {
            taa = !taa;
        }

        if (Input.GetKeyDown(KeyCode.Keypad9))
        {
            if (fogRenderer.atmosphereScattering != 0)
            {
                fogRenderer.atmosphereScattering = 0;
            }
            else
            {
                fogRenderer.atmosphereScattering = 0.0001f;
            }
        }
        if (time > 1)
        {
            time = 0;
        }
    }
}
