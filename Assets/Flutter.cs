using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Flutter : MonoBehaviour
{   
    float startIntensity;
    Light light;
    int i = 0;
    public int updateFreq;
    void Start()
    {
        light = gameObject.GetComponent<Light>();
        startIntensity = light.intensity;
    }

    // Update is called once per frame
    void Update()
    {
        if ( i ++ % updateFreq == 0) {
            float rand = Random.Range(0.75f,1);
            light.intensity = ( rand * startIntensity );
                
            i = 0;
        }
    }
}
