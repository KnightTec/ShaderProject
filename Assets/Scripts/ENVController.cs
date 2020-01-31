using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private float anisotropy;
    private bool depthDither;
    private bool sampleDither;
    private bool analyticFog;

    private void Start()
    {
        noiseIntensity = fogRenderer.noiseIntensity;
        noiseSpeed = fogRenderer.noiseDirection.x;
        taa = fogRenderer.taa;
        anisotropy = fogRenderer.anisotropy;
        depthDither = fogRenderer.ditherDepth;
        sampleDither = fogRenderer.ditheredSampling;
        analyticFog = fogRenderer.analyticFog;
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
            fogRenderer.anisotropy = anisotropy;
            fogRenderer.ditherDepth = depthDither;
            fogRenderer.ditheredSampling = sampleDither;
            fogRenderer.analyticFog = analyticFog;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            SceneManager.LoadScene("woods", LoadSceneMode.Single);
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
                time += Time.deltaTime * 0.03f;
            }
            else if (Input.GetKey(KeyCode.KeypadMinus))
            {
                time -= Time.deltaTime * 0.03f;
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
                fogScatteringMultiplier += Time.deltaTime * 2f;
            }
            else if (Input.GetKey(KeyCode.KeypadMinus))
            {
                fogScatteringMultiplier -= Time.deltaTime * 2f;
            }
        }
        if (Input.GetKey(KeyCode.Keypad5))
        {
            if (Input.GetKey(KeyCode.KeypadPlus))
            {
                noiseIntensity += Time.deltaTime;
                if (noiseIntensity <= 0.98f)
                {
                    fogScatteringMultiplier += Time.deltaTime * 16;
                }
            }
            else if (Input.GetKey(KeyCode.KeypadMinus))
            {
                noiseIntensity -= Time.deltaTime;
                if (noiseIntensity >= 0)
                {
                    fogScatteringMultiplier -= Time.deltaTime * 16;
                }
            }
            noiseIntensity = Mathf.Clamp(noiseIntensity, 0, 0.98f);
        }
        fogScatteringMultiplier = Mathf.Max(fogScatteringMultiplier, 0);
        fogFalloffMultiplier = Mathf.Max(fogFalloffMultiplier, 0);
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
        }
        if (Input.GetKey(KeyCode.Keypad7))
        {
            if (Input.GetKey(KeyCode.KeypadPlus))
            {
                anisotropy += Time.deltaTime;
            }
            else if (Input.GetKey(KeyCode.KeypadMinus))
            {
                anisotropy -= Time.deltaTime;
            }
            anisotropy = Mathf.Clamp(anisotropy, -0.9f, 0.9f);
        }
        if (Input.GetKey(KeyCode.Keypad8))
        {
            if (Input.GetKey(KeyCode.KeypadPlus))
            {
                sunIntensityMultiplier += Time.deltaTime * 10;
            }
            else if (Input.GetKey(KeyCode.KeypadMinus))
            {
                sunIntensityMultiplier -= Time.deltaTime * 10;
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            fogRenderer.scatterColor = Color.white;
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            fogRenderer.scatterColor = new Color(0.25f, 0.5f, 1);
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            fogRenderer.scatterColor = new Color(0.7f, 1.0f, 0.1f);
        }
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            fogRenderer.selfShadow = !fogRenderer.selfShadow;
        }
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            analyticFog = !analyticFog;
        }
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            sampleDither = !sampleDither;
        }
        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            depthDither = !depthDither;
        }
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            taa = !taa;
        }
        if (Input.GetKeyDown(KeyCode.Alpha0))
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
