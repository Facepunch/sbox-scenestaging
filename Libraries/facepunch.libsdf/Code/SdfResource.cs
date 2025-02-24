using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sandbox.Sdf;

/// <summary>
/// References a layer or volume that will be used as a texture when rendering.
/// </summary>
public class TextureReference<T>
	where T : SdfResource<T>
{
	/// <summary>
	/// Material attribute name to set for the materials used by this layer or volume.
	/// </summary>
	public string TargetAttribute { get; set; }

	/// <summary>
	/// Source layer or volume that will provide the texture. The texture will have a single channel,
	/// with 0 representing -<see cref="WorldQuality.MaxDistance"/> of the source layer,
	/// and 1 representing +<see cref="WorldQuality.MaxDistance"/>.
	/// </summary>
	public T Source { get; set; }
}

/// <summary>
/// Base class for SDF volume / layer resources.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class SdfResource<T> : GameResource
	where T : SdfResource<T>
{
#pragma warning disable SB3000
	// ReSharper disable once StaticMemberInGenericType
	private static char[] SplitChars { get; } = { ' ' };
#pragma warning restore SB3000

	/// <summary>
	/// If true, this layer or volume is only used as a texture source by other layers or volumes.
	/// This will disable collision shapes and render mesh generation for this layer or volume.
	/// </summary>
	public bool IsTextureSourceOnly { get; set; }

	/// <summary>
	/// Tags that physics shapes created by this layer or volume should have, separated by spaces.
	/// If empty, no physics shapes will be created.
	/// </summary>
	[Editor( "tags" )]
	[HideIf( nameof( IsTextureSourceOnly ), true )]
	public string CollisionTags { get; set; } = "solid";

	/// <summary>
	/// Array of tags that physics shapes created by this layer or volume should have.
	/// If empty, no physics shapes will be created.
	/// </summary>
	[Hide]
	[JsonIgnore]
	public string[] SplitCollisionTags => IsTextureSourceOnly
		? Array.Empty<string>()
		: CollisionTags?.Split( SplitChars, StringSplitOptions.RemoveEmptyEntries ) ?? Array.Empty<string>();

	/// <summary>
	/// If true, this resource will have a collision mesh. True if <see cref="CollisionTags"/> has any items
	/// and <see cref="IsTextureSourceOnly"/> is false.
	/// </summary>
	[Hide]
	[JsonIgnore]
	public bool HasCollision => !IsTextureSourceOnly && !string.IsNullOrWhiteSpace( CollisionTags );

	/// <summary>
	/// Controls mesh visual quality, affecting performance and networking costs.
	/// </summary>
	public WorldQualityPreset QualityLevel { get; set; } = WorldQualityPreset.Medium;

	/// <summary>
	/// How many rows / columns of samples are stored per chunk.
	/// Higher means more needs to be sent over the network, and more work for the mesh generator.
	/// Medium quality is 16 for 2D layers.
	/// </summary>
	[ShowIf( nameof( QualityLevel ), WorldQualityPreset.Custom )]
	public int ChunkResolution { get; set; } = 16;

	/// <summary>
	/// How wide / tall a chunk is in world space. If you'll always make small
	/// edits to this layer, you can reduce this to add detail.
	/// Medium quality is 256 for 2D layers.
	/// </summary>
	[ShowIf( nameof( QualityLevel ), WorldQualityPreset.Custom )]
	public float ChunkSize { get; set; } = 256f;

	/// <summary>
	/// Largest absolute value stored in a chunk's SDF.
	/// Higher means more samples are written to when doing modifications.
	/// I'd arbitrarily recommend ChunkSize / ChunkResolution * 4.
	/// </summary>
	[ShowIf( nameof( QualityLevel ), WorldQualityPreset.Custom )]
	public float MaxDistance { get; set; } = 64f;

	/// <summary>
	/// References to layers or volumes that will be used as textures when rendering this layer or volume.
	/// All referenced layers or volumes must have the same chunk size as this layer or volume.
	/// </summary>
	[HideIf( nameof( IsTextureSourceOnly ), true )]
	public List<TextureReference<T>> ReferencedTextures { get; set; }

	[Hide]
	[JsonIgnore]
	internal WorldQuality Quality => QualityLevel switch
	{
		WorldQualityPreset.Custom => new WorldQuality( ChunkResolution, ChunkSize, MaxDistance ),
		_ => GetQualityFromPreset( QualityLevel )
	};

	internal abstract WorldQuality GetQualityFromPreset( WorldQualityPreset preset );

	[Hide]
	[JsonIgnore]
	internal int ChangeCount { get; private set; }

	protected override void PostReload()
	{
		base.PostReload();

		++ChangeCount;
	}
}
