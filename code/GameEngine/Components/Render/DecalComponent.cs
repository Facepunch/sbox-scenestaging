using Sandbox;
using Sandbox.Diagnostics;

/// <summary>
/// Component that creates a projected decal relative to its GameObject.
/// </summary>
[Title( "Decal" )]
[Icon( "lens_blur", "red", "white" )]
[EditorHandle( "materials/gizmo/decal.png" )]
public class DecalComponent : BaseComponent, BaseComponent.ExecuteInEditor
{
	ProjectedDecalSceneObject _sceneObject;

	/// <summary>
	/// The material to use for this decal
	/// </summary>
	[Property] public Material Material { get; set; }

	/// <summary>
	/// The size of the decal. x being width, y being the height, z being the projection distance
	/// </summary>
	[Property] public Vector3 Size { get; set; } = new Vector3( 32, 32, 256 );
	
	/// <summary>
	/// Tint the decal
	/// </summary>
	[Property] public Color TintColor { get; set; } = Color.White;

	public override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected )
		{
			Gizmo.Draw.Line( Vector3.Zero, Vector3.Down * Size.z );
		}
		else
		{
			Gizmo.Draw.LineBBox( new BBox( new Vector3( -Size.x / 2f, -Size.y / 2f, -Size.z ), new Vector3( Size.x / 2f, Size.y / 2f, 0 ) ) );
		}
	}

	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		if ( Material == null ) return;

		_sceneObject = new ProjectedDecalSceneObject( Scene.SceneWorld, Material, Size );
	}

	public override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
	}

	int hash = 0;
	protected int Hash => HashCode.Combine( Size, Material );

	protected override void OnPreRender()
	{
		if ( !_sceneObject.IsValid() )
			return;

		if ( Hash != hash )
		{
			_sceneObject.Material = Material;
			_sceneObject.Size = Size;
			hash = Hash;
		}

		_sceneObject.ColorTint = TintColor;
		_sceneObject.Transform = Transform.World;
	}
}
