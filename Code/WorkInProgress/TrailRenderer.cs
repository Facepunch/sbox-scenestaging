
namespace Sandbox;

[Title( "Trail Renderer" )]
[Category( "Rendering" )]
[Icon( "show_chart" )]
public sealed class TrailRenderer : Component, Component.ExecuteInEditor
{
	SceneTrailObject _so;

	[Group( "Trail" )]
	[Property] public int MaxPoints { get; set; } = 64;

	[Group( "Trail" )]
	[Property] public float PointDistance { get; set; } = 8;

	[Group( "Trail" )]
	[Property] public float LifeTime { get; set; } = 2;

	[Group( "Appearance" )]
	[Property] public TrailTextureConfig Texturing { get; set; } = TrailTextureConfig.Default;

	[Group( "Appearance" )]
	[Property] public Gradient Color { get; set; } = global::Color.Cyan;

	[Group( "Appearance" )]
	[Property] public Curve Width { get; set; } = 5;

	[Group( "Rendering" )]
	[Property] public bool Wireframe { get; set; }

	[Group( "Rendering" )]
	[Property] public bool Opaque { get; set; } = true;

	[ShowIf( "Opaque", true )]
	[Group( "Rendering" )]
	[Property] public bool CastShadows { get; set; } = false;

	[ShowIf( "Opaque", false )]
	[Group( "Rendering" )]
	[Property] public BlendMode BlendMode { get; set; } = BlendMode.Normal;

	protected override void OnEnabled()
	{
		_so = new SceneTrailObject( Scene.SceneWorld );
		_so.Transform = Transform.World;
	}

	protected override void OnDisabled()
	{
		_so?.Delete();
		_so = null;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		_so.Transform = new Transform( Transform.Position );
		_so.LifeTime = LifeTime;
		_so.Texturing = Texturing;
		_so.MaxPoints = MaxPoints;
		_so.PointDistance = PointDistance;
		_so.TrailColor = Color;
		_so.Width = Width;
		_so.Flags.CastShadows = Opaque && CastShadows;
		_so.Wireframe = Wireframe;
		_so.Opaque = Opaque;
		_so.BlendMode = BlendMode;
		_so.LineTexture = Texturing.Texture ?? Texture.White;
		_so.TryAddPosition( Transform.Position );
		_so.AdvanceTime( Time.Delta );
		_so.Build();
	}
}
