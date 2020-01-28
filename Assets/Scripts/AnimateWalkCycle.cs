using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimateWalkCycle : MonoBehaviour
{
    public float depth, speed;
    public Vector3 firstAxis;
    public Vector3 secondAxis;
   
    // Update is called once per frame
    void Update()
    {
        transform.Translate(
            depth * Mathf.Sin(speed * Time.time) * firstAxis
            + depth * Mathf.Cos(speed * Time.time) * secondAxis, Space.Self);
    }
}
