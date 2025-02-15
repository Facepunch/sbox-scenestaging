using NativeEngine;
using static Sandbox.Component;

namespace Sandbox;


[Category( "Rendering" )]
[Icon( "cloud_circle" )]
public sealed partial class VolumetricMaterialRenderer : Renderer, ExecuteInEditor
{
    /// <summary>
    /// The volumetric material to render.
    /// </summary>
    [Property, MakeDirty] public NanoVDB VDB { get; set; }

    ComputeBuffer<uint> Buffer;

	internal SceneObject _sceneObject;
	public SceneObject SceneObject => _sceneObject;

	protected override void OnEnabled()
    {
		if ( VDB is null )
			return;

		Buffer = new( VDB.Grids[0].Data.Count );
        //Buffer.SetData( Material.VDB.Grids[0].Data );

        var model = CreateModel();
		_sceneObject = new( Scene.SceneWorld, model, Transform.World );
		Transform.OnTransformChanged += OnTransformChanged;
	}

	private void OnTransformChanged()
	{
		if ( _sceneObject.IsValid() )
			_sceneObject.Transform = Transform.World;
	}

	private Model CreateModel()
        {
            var modelBuilder = new ModelBuilder();

            var material = Sandbox.Material.FromShader( "volumetric" );

            var mesh = new Mesh(material);
            mesh.Bounds = VDB.BoundingBox;

            var vb = new VertexBuffer();

            vb.Init( true );

			Vector3 min = VDB.BoundingBox.Mins;
			Vector3 max = VDB.BoundingBox.Maxs;

			// Define the vertices of the cube
			var vertices = new Vector3[]
			{
				// Front face
				new Vector3(min.x, min.y, max.z),  // Bottom-left front (0)
				new Vector3(max.x, min.y, max.z),  // Bottom-right front (1)
				new Vector3(max.x, max.y, max.z),  // Top-right front (2)
				new Vector3(min.x, max.y, max.z),  // Top-left front (3)

				// Back face
				new Vector3(min.x, min.y, min.z),  // Bottom-left back (4)
				new Vector3(max.x, min.y, min.z),  // Bottom-right back (5)
				new Vector3(max.x, max.y, min.z),  // Top-right back (6)
				new Vector3(min.x, max.y, min.z)   // Top-left back (7)
			};

			// Define the indices for the triangles that make up the cube
			var indices = new int[]
			{
				// Front face
				0, 1, 2,    // First triangle
				2, 3, 0,    // Second triangle

				// Back face
				4, 6, 5,    // First triangle
				6, 4, 7,    // Second triangle

				// Left face
				4, 0, 3,    // First triangle
				3, 7, 4,    // Second triangle

				// Right face
				1, 5, 6,    // First triangle
				6, 2, 1,    // Second triangle

				// Top face
				3, 2, 6,    // First triangle
				6, 7, 3,    // Second triangle

				// Bottom face
				4, 5, 1,    // First triangle
				1, 0, 4     // Second triangle
			};

			foreach ( var index in indices )
			{
				vb.AddRawIndex( index );
			}


			foreach ( var vertex in vertices )
			{
				vb.Add( new Vertex { Position = vertex } );
			}

			mesh.CreateBuffers(vb);

            return modelBuilder.AddMesh(mesh).Create();
        }


    protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected )
			return;

        Log.Info( $"Hello World {VDB.BoundingBox}" );

		Gizmo.Draw.LineBBox( VDB.BoundingBox );
	}
}