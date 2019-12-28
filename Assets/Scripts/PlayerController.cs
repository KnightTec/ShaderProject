using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float translationSpeed = 5;
    public float rotationSpeed = 50;

    private Vector3 translation;
    private Vector3 rotation;

    private void Start()
    {
        translation = new Vector3(0, 0, 0);
        rotation = new Vector3(0, 0, 0);
    }

    // Update is called once per frame
    void Update()
    {
        translation.x = Input.GetAxis("Horizontal") * Time.deltaTime * translationSpeed;
        translation.z = Input.GetAxis("Vertical") * Time.deltaTime * translationSpeed;
        transform.Translate(translation, Space.Self);

        rotation.x = -Input.GetAxis("Mouse Y") * Time.deltaTime * rotationSpeed;
        transform.Rotate(rotation);
        rotation.x = 0;
        rotation.y = Input.GetAxis("Mouse X") * Time.deltaTime * rotationSpeed;
        transform.Rotate(rotation, Space.World);
        rotation.y = 0;
    }
}
