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
    private NanoVDB VDB { get; set; }

	[Property, MakeDirty, NVDBPath] public string VDBPath { get; set; }


    List<GpuBuffer<uint>> GridsBuffer = new();

	internal SceneObject _sceneObject;

	protected override void OnDirty()
	{
		base.OnDirty();

		_sceneObject?.Delete();
		OnEnabled();
	}

	protected override void OnEnabled()
    {
		
		VDB = NanoVDB.Load( VDBPath );
		if ( VDB is null )
			return;

		GridsBuffer.Clear();
		foreach ( var grid in VDB.Grids )
		{
			var Buffer = new GpuBuffer<uint>( grid.Data.Length );
			Buffer.SetData( grid.Data.AsSpan() );
			GridsBuffer.Add( Buffer );
		}

        var model = CreateModel();
		_sceneObject = new( Scene.SceneWorld, model, Transform.World );
		Transform.OnTransformChanged += OnTransformChanged;

		_sceneObject.Attributes.Set( "GridBuffer", GridsBuffer[0] );
	}

	protected override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
	}

	private void OnTransformChanged()
	{
		if ( _sceneObject.IsValid() )
			_sceneObject.Transform = Transform.World;
	}

	private Model CreateModel()
	{
		// Create material and mesh with the cube bounds
		var material = Material.FromShader("volumetric");
		var mesh = new Mesh(material) { Bounds = VDB.BoundingBox };

		// Initialize and prepare the vertex buffer
		var vb = new VertexBuffer();
		vb.Init(true);

		var min = VDB.BoundingBox.Mins;
		var max = VDB.BoundingBox.Maxs;

		// Define cube vertices (front and back faces)
		var vertices = new[]
		{
			new Vector3(min.x, min.y, max.z), // Front-bottom-left
			new Vector3(max.x, min.y, max.z), // Front-bottom-right
			new Vector3(max.x, max.y, max.z), // Front-top-right
			new Vector3(min.x, max.y, max.z), // Front-top-left
			new Vector3(min.x, min.y, min.z), // Back-bottom-left
			new Vector3(max.x, min.y, min.z), // Back-bottom-right
			new Vector3(max.x, max.y, min.z), // Back-top-right
			new Vector3(min.x, max.y, min.z)  // Back-top-left
		};

		// Define cube triangle indices
		var indices = new[]
		{
			// Front face
			0, 1, 2, 2, 3, 0,
			// Back face
			4, 6, 5, 6, 4, 7,
			// Left face
			4, 0, 3, 3, 7, 4,
			// Right face
			1, 5, 6, 6, 2, 1,
			// Top face
			3, 2, 6, 6, 7, 3,
			// Bottom face
			4, 5, 1, 1, 0, 4
		};

		// Add indices and vertices to the buffer
		foreach (var index in indices)
		{
			vb.AddRawIndex(index);
		}

		foreach (var vertex in vertices)
		{
			vb.Add(new Vertex { Position = vertex });
		}

		mesh.CreateBuffers(vb);

		// Build and return the model
		return new ModelBuilder().AddMesh(mesh).Create();
	}


    protected override void DrawGizmos()
	{
		if( VDB is null )
			return;

		if ( !Gizmo.IsSelected )
			return;

		Gizmo.Draw.LineBBox( VDB.BoundingBox );
	}
}