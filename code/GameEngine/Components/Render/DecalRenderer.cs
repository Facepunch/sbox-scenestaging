using Sandbox;
using Sandbox.Diagnostics;

/// <summary>
/// Component that creates a projected decal relative to its GameObject.
/// </summary>
[Title( "Decal Renderer" )]
[Category( "Rendering" )]
[Icon( "lens_blur" )]
[EditorHandle( "materials/gizmo/decal.png" )]
public class DecalRenderer : Renderer, Component.ExecuteInEditor
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

	/// <summary>
	/// Triplanar - projects in multiple directions
	/// </summary>
	[Property] public bool TriPlanar { get; set; } = false;

	/// <summary>
	/// Triplanar - projects in multiple directions
	/// </summary>
	[Property] public bool Mod2XBlending { get; set; } = false;

	[Property, Range( 0, 180)] public float CutoffAngle { get; set; } = 60;
	[Property, Range( 0, 50 )] public float CutoffAngleSoftness { get; set; } = 5;

	protected override void DrawGizmos()
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

	protected override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		if ( Material == null ) return;

		_sceneObject = new ProjectedDecalSceneObject( Scene.SceneWorld, Material, Size );
	}

	protected override void OnDisabled()
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
		_sceneObject.Attributes.Set( "g_bTriPlanar", TriPlanar );
		_sceneObject.Attributes.Set( "g_flCutoffAngle", CutoffAngle );
		_sceneObject.Attributes.Set( "g_flCutoffAngleSoftness", CutoffAngleSoftness );
		_sceneObject.Attributes.SetCombo( "D_BLEND_MODE", Mod2XBlending ? 1 : 0 );
		_sceneObject.Flags.NeedsLightProbe = true;
		_sceneObject.Flags.NeedsEnvironmentMap = true;


	}
}
