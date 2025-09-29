using System.Text.Json.Nodes;
using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Applies a tonemapping effect to the camera.
/// </summary>
[Title( "Tone Mapping" )]
[Category( "Post Processing" )]
[Icon( "exposure" )]
public class Tonemapping2 : BasePostProcess<Tonemapping2>
{
	/// <summary>
	/// Options to select a tonemapping algorithm to use for color grading.
	/// </summary>
	public enum TonemappingMode
	{
		/// <summary>
		/// John Hable's filmic tonemapping algorithm.
		/// Matches the default curve Source 2 uses based on Uncharted 2.
		/// </summary>
		HableFilmic = 1,
		/// <summary>
		/// The most realistic tonemapper at handling bright light, desaturating light as it becomes brighter.
		/// This is slightly more expensive than other options.
		/// </summary>
		ACES,
		/// <summary>
		/// Reinhard's tonemapper, which is a simple and fast tonemapper.
		/// </summary>
		ReinhardJodie,
		/// <summary>
		/// Linear tonemapper, only applies autoexposure.
		/// </summary>
		Linear,
		/// <summary>
		/// Default AgX implementation
		/// </summary>
		AgX
	}

	public enum ExposureColorSpaceEnum
	{
		RGB,
		Luminance
	}

	/// <summary>
	/// Which tonemapping algorithm to use for color grading.
	/// </summary>
	[Property, MakeDirty] public TonemappingMode Mode { get; set; } = TonemappingMode.HableFilmic;

	[Obsolete] public float ExposureBias { get; set; }

	[ShowIf( nameof( Mode ), TonemappingMode.HableFilmic )]
	[Property, MakeDirty] public ExposureColorSpaceEnum ExposureMethod { get; set; } = ExposureColorSpaceEnum.RGB;

	CommandList cl = new CommandList( "ChromaticAberration" );
	RenderAttributes attr = new();

	public override void Build( Context ctx )
	{
		var shader = Material.FromShader( "shaders/tonemapping/tonemapping.shader" );

		cl.Reset();

		attr.SetComboEnum( "D_TONEMAPPING", Mode );
		attr.SetComboEnum( "D_EXPOSUREMETHOD", ExposureMethod );

		cl.Attributes.GrabFrameTexture( "ColorBuffer" );
		cl.Blit( shader, attr );

		ctx.Add( cl, Stage.Tonemapping );

		UpdateExposure( ctx.Camera, ctx );
	}

	//
	// All of this auto exposure stuff should be it's own component
	// It's used by tonemapping, not part of it
	//
	[Property, Group( "Auto Exposure" )]
	public bool AutoExposureEnabled { get; set; } = true;

	[Property, Group( "Auto Exposure" ), Range( 0.0f, 3.0f ), ShowIf( nameof( AutoExposureEnabled ), true )]
	public float MinimumExposure { get; set; } = 1.0f;

	[Property, Group( "Auto Exposure" ), Range( 0.0f, 5.0f ), ShowIf( nameof( AutoExposureEnabled ), true )]
	public float MaximumExposure { get; set; } = 3.0f;

	[Property, Group( "Auto Exposure" ), Range( -5.0f, 5.0f ), ShowIf( nameof( AutoExposureEnabled ), true )]
	public float ExposureCompensation { get; set; } = 0.0f;

	[Property, Group( "Auto Exposure" ), Range( 1.0f, 10.0f ), ShowIf( nameof( AutoExposureEnabled ), true )]
	public float Rate { get; set; } = 1.0f;

	void UpdateExposure( CameraComponent camera, Context ctx )
	{
		if ( !camera.IsValid() ) return;

		camera.AutoExposure.Enabled = AutoExposureEnabled;
		camera.AutoExposure.Compensation = ctx.GetWeighted( x => x.ExposureCompensation, 0 );
		camera.AutoExposure.MinimumExposure = ctx.GetWeighted( x => x.MinimumExposure, 1 );
		camera.AutoExposure.MaximumExposure = ctx.GetWeighted( x => x.MaximumExposure, 3 );
		camera.AutoExposure.Rate = ctx.GetWeighted( x => x.Rate, 1 );
	}

}
