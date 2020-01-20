using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomUpRot : MonoBehaviour
{
    public GameObject[] treefabs;
    // Start is called before the first frame update
    void Start()
    {
        foreach ( Transform t in transform ) {
            t.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        } 
    }
}
