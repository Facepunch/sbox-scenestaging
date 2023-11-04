using Sandbox;
using Sandbox.Diagnostics;
using System;

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
	/// Width, Height, Projection distance
	/// </summary>
	[Property] public Vector3 DecalScale { get; set; } = new Vector3( 32, 32, 256 );
	
	/// <summary>
	/// Tint the decal
	/// </summary>
	[Property] public Color TintColor { get; set; } = Color.White;

	public override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected )
		{
			Gizmo.Draw.Line( Vector3.Zero, Vector3.Down * DecalScale.z );
		}
		else
		{
			Gizmo.Draw.LineBBox( new BBox( new Vector3( -DecalScale.x / 2f, -DecalScale.y / 2f, -DecalScale.z ), new Vector3( DecalScale.x / 2f, DecalScale.y / 2f, 0 ) ) );
		}
	}

	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		if ( Material == null ) return;

		_sceneObject = new ProjectedDecalSceneObject( Scene.SceneWorld, Material, DecalScale );
	}

	public override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
	}

	int hash = 0;

	/// <summary>
	/// Decals currently don't support changing their scale / material without recreating the object
	/// </summary>
	protected int Hash => HashCode.Combine( DecalScale, Material );

	protected override void OnPreRender()
	{
		if ( !_sceneObject.IsValid() )
			return;

		if ( Hash != hash )
		{
			_sceneObject.Material = Material;
			_sceneObject.DecalSize = DecalScale;
			hash = Hash;
		}

		_sceneObject.ColorTint = TintColor;
		_sceneObject.Transform = Transform.World;
	}
}
