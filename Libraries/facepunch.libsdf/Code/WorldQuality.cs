namespace Sandbox.Sdf;

/// <summary>
/// Preset quality settings for <see cref="SdfArray{TSdf}"/>.
/// </summary>
public enum WorldQualityPreset
{
	/// <summary>
	/// Cheap and cheerful, suitable for frequent (per-frame) edits.
	/// </summary>
	Low,

	/// <summary>
	/// Recommended quality for most cases.
	/// </summary>
	Medium,

	/// <summary>
	/// More expensive to update and network, but a much smoother result.
	/// </summary>
	High,

	/// <summary>
	/// Only use this for small, detailed objects!
	/// </summary>
	Extreme,

	/// <summary>
	/// Manually tweak quality parameters.
	/// </summary>
	Custom = -1
}

/// <summary>
/// Quality settings for <see cref="SdfArray{TSdf}"/>.
/// </summary>
public record struct WorldQuality( int ChunkResolution, float ChunkSize, float MaxDistance )
{
	/// <summary>
	/// Distance between samples in one axis.
	/// </summary>
	public float UnitSize => ChunkSize / ChunkResolution;

	/// <summary>
	/// Read an instance of <see cref="WorldQuality"/> from a <see cref="NetRead"/>er.
	/// </summary>
	public static WorldQuality Read( ref ByteStream net )
	{
		return new WorldQuality( net.Read<int>(),
			net.Read<float>(),
			net.Read<float>() );
	}

	/// <summary>
	/// Write this instance to a <see cref="NetWrite"/>er. Can be read with <see cref="Read"/>.
	/// </summary>
	public void Write( ref ByteStream net )
	{
		net.Write( ChunkResolution );
		net.Write( ChunkSize );
		net.Write( MaxDistance );
	}
}
