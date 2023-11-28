using Sandbox.Diagnostics;

namespace Sandbox.TerrainEngine;

[Title( "Terrain" )]
[Category( "Rendering" )]
[Icon( "terrain" )]
public partial class Terrain : BaseComponent, BaseComponent.ExecuteInEditor
{
	SceneObject _sceneObject;
	public SceneObject SceneObject => _sceneObject;

	[Property] public TerrainData TerrainData { get; set; }

	/// <summary>
	/// This needs to be a material that uses a terrain shader.
	/// </summary>
	[Property] public Material TerrainMaterial { get; set; }

	[Property] public float MaxHeightInInches { get; set; } = 40000.0f;
	[Property] public float TerrainResolutionInInches { get; set; } = 39.0f;

	[Property, Category( "Clipmap" )] public int ClipMapLodLevels { get; set; } = 7;
	[Property, Category( "Clipmap" )] public int ClipMapLodExtentTexels { get; set; } = 128;

	[Property, Category( "Debug" )] public DebugViewEnum DebugView { get; set; } = DebugViewEnum.None;

	Model _model;

	int vertexCount = 0;
	int indexCount = 0;

	private Texture _heightmap;

	public float GetHeight( Vector3 pos )
	{
		// this should really interpolate, but just round to ints for now
		int x = (int)Math.Round( pos.x / TerrainResolutionInInches );
		int y = (int)Math.Round( pos.y / TerrainResolutionInInches );

		return ((float)TerrainData.GetHeight( x, y ) / (float)ushort.MaxValue ) * MaxHeightInInches;
	}

	public bool InRange( Vector3 pos )
	{
		// this should really interpolate, but just round to ints for now
		int x = (int)Math.Round( pos.x / TerrainResolutionInInches );
		int y = (int)Math.Round( pos.y / TerrainResolutionInInches );

		return TerrainData.InRange( x, y );
	}

	public bool RayIntersects( Ray ray, out Vector3 position )
	{
		var raycastDistance = 10000.0f;
		for ( var distance = 0.0f; distance <= raycastDistance; distance += 0.1f )
		{
			var currentPoint = ray.Position + ray.Forward * distance;

			if ( !InRange( currentPoint ) )
				continue;

			if ( currentPoint.z < -0.1f )
				break;

			float height = GetHeight( currentPoint );
			if ( currentPoint.z <= height )
			{
				currentPoint.z = height;
				position = currentPoint;
				return true;
			}
		}

		position = Vector3.Zero;
		return false;
	}

	public void SyncHeightMap()
	{
		if ( TerrainData is null ) return;

		if ( _heightmap == null )
		{
			_heightmap = Texture.Create( TerrainData.HeightMapWidth, TerrainData.HeightMapHeight, ImageFormat.R16 )
				.WithData( new ReadOnlySpan<ushort>( TerrainData.HeightMap ) )
				.WithDynamicUsage() // maybe
				.WithUAVBinding() // compute mips?
				.WithName( "terrain_heightmap" )
				.Finish();
			return;
		}

		// TODO: We could update only the dirty region, but this seems reasonable at least on 513x513
		_heightmap.Update( new ReadOnlySpan<ushort>( TerrainData.HeightMap ) );
	}

	BrushPreviewSceneObject _brushSceneObject;

	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		{
			var clipmapMesh = GeometryClipmap.GenerateMesh_DiamondSquare( ClipMapLodLevels, ClipMapLodExtentTexels, TerrainMaterial );
			_model = Model.Builder.AddMesh( clipmapMesh ).Create();

			vertexCount = clipmapMesh.VertexCount;
			indexCount = clipmapMesh.IndexCount;
		}

		_sceneObject = new SceneObject( Scene.SceneWorld, _model, Transform.World );
		_sceneObject.Tags.SetFrom( GameObject.Tags );
		_sceneObject.Batchable = false;
		_sceneObject.Flags.CastShadows = false;

		_brushSceneObject = new BrushPreviewSceneObject( Scene.SceneWorld );
		_brushSceneObject.Batchable = false;
		_brushSceneObject.Flags.CastShadows = false;

		Brush = "circle0";

		SyncHeightMap();
	}

	public override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;

		_brushSceneObject?.Delete();
		_brushSceneObject = null;
	}

	protected override void OnPreRender()
	{
		if ( !_sceneObject.IsValid() )
			return;

		_sceneObject.Transform = Transform.World;

		_sceneObject.Attributes.Set( "HeightMap", _heightmap );
		_sceneObject.Attributes.Set( "HeightScale", MaxHeightInInches );
		_sceneObject.Attributes.Set( "TerrainResolution", TerrainResolutionInInches );
		_sceneObject.Attributes.Set( "DebugView", (int)DebugView );
	}

	public override void DrawGizmos()
	{
		if ( RayIntersects( Gizmo.CurrentRay, out var hitPosition ) )
		{
			Gizmo.Hitbox.TrySetHovered( hitPosition );

			Gizmo.Draw.Color = Color.White;

			_brushSceneObject.Radius = BrushRadius * TerrainResolutionInInches;
			_brushSceneObject.Transform = new Transform( hitPosition );
			_brushSceneObject.Texture = BrushTexture;

			if ( Gizmo.IsPressed )
			{

				Gizmo.Draw.ScreenText( "pressed", Vector2.One * 16 );
				AddHeight( hitPosition );
				SyncHeightMap();
			}
		}

		//Gizmo.Draw.ScreenText( $"Terrain Size: {HeightMap.Width * TerrainResolutionInInches} x {HeightMap.Height * TerrainResolutionInInches} ( {(HeightMap.Width * TerrainResolutionInInches).InchToMeter()}m² )", Vector2.One * 16, size: 16, flags: TextFlag.Left );
		//Gizmo.Draw.ScreenText( $"Clipmap Lod Levels: {ClipMapLodLevels} covering {ClipMapLodExtentTexels} texels", Vector2.One * 16 + Vector2.Up * 24, size: 16, flags: TextFlag.Left );
		//Gizmo.Draw.ScreenText( $"Clipmap Mesh: {vertexCount.KiloFormat()} verticies {(indexCount / 3).KiloFormat()} triangles", Vector2.One * 16 + Vector2.Up * 48, size: 16, flags: TextFlag.Left );
	}

	public override void EditorUpdate()
	{

	}

	public enum DebugViewEnum
	{
		None = 0,
		LOD = 1,
		Splat = 2,
	}

	public BBox Bounds
	{
		get
		{
			if ( _sceneObject is not null )
			{
				return _sceneObject.Bounds;
			}

			return new BBox( Transform.Position, 16 );
		}
	}
}
