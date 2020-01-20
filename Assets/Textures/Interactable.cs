using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Interactable : MonoBehaviour
{
    public Text textObject;
    public bool prerequisitMet;
    public Interactable next;
    public Explode exploder;
    public GameObject activator;

    public string interactText;
    public string tipText;

    private bool playerClose = false;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if ( playerClose && ( Input.GetKeyDown(KeyCode.Mouse0) || Input.GetKeyDown(KeyCode.E)) )
        {
            if (next != null)
                next.prerequisitMet = true;
            textObject.text = "";
            Destroy(gameObject);
            if (exploder != null)
                exploder.DoExplode();
            if (activator != null)
                activator.SetActive(true);
        }
    }

    void OnTriggerEnter ( Collider other )
    {
        if (other.gameObject.tag == "Player")
        {
            if ( prerequisitMet )
            {
                textObject.text = interactText;
                playerClose = true;
            }
            else
            {
                textObject.text = tipText;
            }
        }
    }

    void OnTriggerExit ( Collider other )
    {
        playerClose = false;
        textObject.text = "";
    }
}
