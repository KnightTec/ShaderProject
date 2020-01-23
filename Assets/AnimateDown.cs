using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimateDown : MonoBehaviour
{
    float startTime;
    public float startAtSecond;
    public float speed;
    // Start is called before the first frame update
    void Start()
    {
        startTime = Time.time;
    }

    // Update is called once per frame
    void Update()
    {
        if ( Time.time - startTime > startAtSecond )
        {
            transform.Translate(Vector3.down * Time.deltaTime * speed, Space.World);
        }
        if (Time.time - startTime > 15)
        {
            Destroy(gameObject);
        }
    }
}
