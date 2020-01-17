using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainScript : MonoBehaviour
{

    // Start is called before the first frame update
    void Start()
    {
        GetComponent<Terrain>().treeDistance = 100000;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
