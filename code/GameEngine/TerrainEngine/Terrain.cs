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

	[Property] public bool CastShadows { get; set; } = true;

	Model _model;

	int vertexCount = 0;
	int indexCount = 0;

	private Texture _heightmap;

	/// <summary>
	/// Gets the height at the given position defined in world space, relative to the Terrain space.
	/// </summary>
	public float GetHeight( Vector3 position )
	{
		// TODO: Clamp in bounds

		// Scale position to TerrainData
		position /= TerrainResolutionInInches;

		var height = TerrainData.GetInterpolatedHeight( position.x, position.y );
		return height * MaxHeightInInches;
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
		var raycastDistance = 20000.0f;
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
			_heightmap = Texture.Create( TerrainData.HeightMapSize, TerrainData.HeightMapSize, ImageFormat.R16 )
				.WithData( new ReadOnlySpan<ushort>( TerrainData.HeightMap ) )
				.WithDynamicUsage()
				.WithUAVBinding() // if we want to compute mips
				.WithName( "terrain_heightmap" )
				.Finish();
			return;
		}

		// TODO: We could update only the dirty region, but this seems reasonable at least on 513x513
		_heightmap.Update( new ReadOnlySpan<ushort>( TerrainData.HeightMap ) );
	}

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

		SyncHeightMap();
	}

	public override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
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
		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.Color = Color.White.WithAlpha( 0.4f );
			Gizmo.Draw.LineBBox( Bounds );
		}

		if ( RayIntersects( Gizmo.CurrentRay, out var hitPosition ) )
		{
			Gizmo.Hitbox.TrySetHovered( hitPosition );
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
			var size = new Vector3( TerrainData.HeightMapSize * TerrainResolutionInInches, TerrainData.HeightMapSize * TerrainResolutionInInches, MaxHeightInInches );
			return new BBox( Vector3.Zero, Transform.Position + size );
		}
	}
}
