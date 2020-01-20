using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Explode : MonoBehaviour
{
    public float force;
    public float radius;
    public float upwards;

    public void DoExplode ()
    {
        foreach ( Transform t in transform )
        {
            t.gameObject.SetActive(true);
            Rigidbody rb = t.gameObject.GetComponent<Rigidbody>();
            rb.useGravity = true;
            rb.AddExplosionForce( force, transform.position, radius, upwards, ForceMode.Force );
        }
    }
}
