using Sandbox;
using Sandbox.Diagnostics;
using System;

[Title( "Decal" )]
[Icon( "lens_blur", "red", "white" )]
[EditorHandle( "materials/gizmo/decal.png" )]
public class DecalComponent : BaseComponent, BaseComponent.ExecuteInEditor
{
	SceneObject _sceneObject;

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
		Gizmo.Draw.Line( Vector3.Zero, Vector3.Forward * DecalScale.z );
	}

	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		PlaceDecal();
	}

	protected void PlaceDecal()
	{
		_sceneObject?.Delete();

		if ( Material == null ) 
			return;
		
		_sceneObject = Decal.Place(
			Scene.SceneWorld, Material, GameObject.Transform.Position, GameObject.Transform.Rotation,
			DecalScale * GameObject.Transform.Scale, TintColor
		);
	}

	public override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
	}

	// tony: From my first look, decals don't really like to be moved, their SceneObject.Transform is a relative transform I believe
	// - Can probably get rid of CProjectedDecal in the end too and move most of it to C#
	// - Need a better way to translate the decal which doesn't mean recreating it entirely
	int hash = 0;
	protected int Hash => HashCode.Combine( GameObject.Transform.Position, GameObject.Transform.Rotation, DecalScale, Material, TintColor );

	protected override void OnPreRender()
	{
		if ( !_sceneObject.IsValid() )
			return;

		var lastHash = hash;
		hash = Hash;

		if ( Hash == lastHash )
			return;

		PlaceDecal();
	}
}
