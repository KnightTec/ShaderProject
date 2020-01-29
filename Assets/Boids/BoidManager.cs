using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BoidManager : MonoBehaviour
{
	[System.Serializable]
	public struct Size {
		public float x, y, z;
    }

	[System.Serializable]
	public struct BoidSettings {
		public int amount;
		public Mesh model;
		public Material material;
		public Size size;
        public float speed;

	}

	public BoidSettings boidSettings;
	public Size size;
	public ComputeShader logic;

	[HideInInspector]
	Bounds bounds;
	ComputeBuffer argBuffer;
	ComputeBuffer dataBuffer;
	int logicHandel;

    struct Boid
    {
        public Boid(Matrix4x4 m, Vector3 heading)
        {
            this.m = m;
            this.heading = heading;
        }

        Matrix4x4 m;
        Vector3 heading;
    }

    // Start is called before the first frame update
    void Start()
    {
		argBuffer = new ComputeBuffer( 1, 5 * sizeof( uint ), ComputeBufferType.IndirectArguments );
		argBuffer.SetData( new uint[]{ boidSettings.model.GetIndexCount( 0 ), (uint)boidSettings.amount * 64, boidSettings.model.GetIndexStart( 0 ), boidSettings.model.GetBaseVertex( 0 ), (uint)0});

		bounds = new Bounds( transform.position, new Vector3( size.x, size.y, size.z ));

        List<Boid> boids = new List<Boid>();
		for( int i = 0; i < boidSettings.amount * 64; ++i ){
			float sizeMod = Random.Range( 0.0f, 1.0f );
			boids.Add( new Boid( Matrix4x4.TRS( 
					new Vector3( 
						Random.Range( -size.x * 0.5f, size.x * 0.5f ),
						Random.Range( -size.y * 0.5f, size.y * 0.5f ),
						Random.Range( -size.z * 0.5f, size.z * 0.5f )),
					Quaternion.Euler(90, 0, 0),
					new Vector3( 
						boidSettings.size.x * sizeMod,
						boidSettings.size.y * sizeMod,
						boidSettings.size.z * sizeMod )), new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f))));
		}

		dataBuffer = new ComputeBuffer( boidSettings.amount * 64, 19 * sizeof( float ));
		dataBuffer.SetData( boids );

		logicHandel = logic.FindKernel( "CSMain" );
		logic.SetBuffer( logicHandel, "Boids", dataBuffer );
        logic.SetInt("boid_amount", boidSettings.amount * 64);
        logic.SetVector("size", new Vector4(size.x, size.y, size.z, 0));
        logic.SetVector("position", transform.position);
        logic.SetFloat("speed", boidSettings.speed);

		boidSettings.material.SetBuffer( "dataBuffer", dataBuffer );
    }

    // Update is called once per frame
    void Update()
    {
		logic.Dispatch( logicHandel, boidSettings.amount, 1, 1 );
		Graphics.DrawMeshInstancedIndirect( boidSettings.model, 0, boidSettings.material, bounds, argBuffer, 0, null, ShadowCastingMode.Off, false );
    }

	void OnDisable()
	{
		if( argBuffer != null )
			argBuffer.Release();
		argBuffer = null;

		if( dataBuffer != null )
			dataBuffer.Release();
		dataBuffer = null;
	}

	void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube( transform.position, new Vector3( size.x, size.y, size.z ));
    }
}