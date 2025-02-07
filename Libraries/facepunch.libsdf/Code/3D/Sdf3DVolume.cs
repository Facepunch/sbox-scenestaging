using System;
using System.Collections.Generic;

namespace Sandbox.Sdf;

/// <summary>
/// Controls the appearance and physical properties of a volume in a <see cref="Sdf3DWorld"/>.
/// </summary>
[GameResource( "SDF 3D Volume", "sdfvol", $"Properties of a volume in a Sdf3DWorld", Icon = "view_in_ar" )]
public class Sdf3DVolume : SdfResource<Sdf3DVolume>
{
	/// <summary>
	/// Material used to render this volume.
	/// </summary>
	[HideIf( nameof( IsTextureSourceOnly ), true )]
	public Material Material { get; set; }

	internal override WorldQuality GetQualityFromPreset( WorldQualityPreset preset )
	{
		switch ( preset )
		{
			case WorldQualityPreset.Low:
				return new( 8, 512f, 96f );

			case WorldQualityPreset.Medium:
				return new( 16, 512f, 48f );

			case WorldQualityPreset.High:
				return new( 32, 512f, 24f );

			case WorldQualityPreset.Extreme:
				return new( 16, 256f, 24f );

			default:
				throw new NotImplementedException();
		}
	}
}
