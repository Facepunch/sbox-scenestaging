using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sandbox.Sdf;

/// <summary>
/// Base class for networked arrays containing raw SDF samples as bytes.
/// </summary>
/// <typeparam name="TSdf">Interface for SDFs that can modify the array</typeparam>
public abstract partial class SdfArray<TSdf> : IDisposable
	where TSdf : ISdf<TSdf>
{
	/// <summary>
	/// How far outside the chunk boundary should samples be stored.
	/// This is used to ensure generated normals are smooth when on a chunk boundary.
	/// </summary>
	public const int Margin = 1;

	/// <summary>
	/// Spacial dimensions of the array (2D / 3D).
	/// </summary>
	public int Dimensions { get; }

	/// <summary>
	/// Quality settings, affecting the resolution of the array.
	/// </summary>
	public WorldQuality Quality { get; private set; }

	/// <summary>
	/// Actual raw samples, encoded as bytes. A value of 0 is -<see cref="WorldQuality.MaxDistance"/>,
	/// 255 is +<see cref="WorldQuality.MaxDistance"/>, and 127.5 is on the surface.
	/// </summary>
	public byte[] BackBuffer { get; private set; }
	public byte[] FrontBuffer { get; private set; }

	/// <summary>
	/// Number of samples stored in one dimension of this array.
	/// </summary>
	public int ArraySize { get; private set; }

	/// <summary>
	/// Total number of samples stored in the entire array. Equal to <see cref="ArraySize"/> to the
	/// power of <see cref="Dimensions"/>.
	/// </summary>
	public int SampleCount { get; private set; }

	/// <summary>
	/// Distance between samples in one axis.
	/// </summary>
	protected float UnitSize { get; private set; }

	/// <summary>
	/// Inverse of <see cref="UnitSize"/>.
	/// </summary>
	protected float InvUnitSize { get; private set; }

	/// <summary>
	/// Inverse of <see cref="WorldQuality.MaxDistance"/>.
	/// </summary>
	protected float InvMaxDistance { get; private set; }

	private bool _textureInvalid = true;
	private Texture _texture;

	/// <summary>
	/// Creates an array with a given number of spacial dimensions.
	/// </summary>
	/// <param name="dimensions">Spacial dimensions of the array (2D / 3D).</param>
	protected SdfArray( int dimensions )
	{
		Dimensions = dimensions;
	}

	protected void SwapBuffers()
	{
		Array.Copy( BackBuffer, FrontBuffer, BackBuffer.Length );
	}

	/// <summary>
	/// Gets the min and max index for a local-space range of samples, clamped to the array bounds.
	/// </summary>
	/// <param name="localMin">Minimum position in local-space along the axis</param>
	/// <param name="localMax">Maximum position in local-space along the axis</param>
	/// <returns>Minimum (inclusive) and maximum (exclusive) indices</returns>
	protected (int Min, int Max, float LocalMin, float LocalMax) GetSampleRange( float localMin, float localMax )
	{
		var min = Math.Max( 0, (int)MathF.Ceiling( (localMin - Quality.MaxDistance) * InvUnitSize ) + Margin );
		var max = Math.Min( ArraySize, (int)MathF.Ceiling( (localMax + Quality.MaxDistance) * InvUnitSize ) + Margin );

		localMin = (min - Margin) * UnitSize;
		localMax = (max - Margin) * UnitSize;

		return (min, max, localMin, localMax);
	}

	/// <summary>
	/// Encodes a distance value to a byte.
	/// </summary>
	/// <param name="distance">Distance to encode.</param>
	/// <returns>-<see cref="WorldQuality.MaxDistance"/> encodes to 0,
	/// +<see cref="WorldQuality.MaxDistance"/> encodes to 255, and therefore 0 becomes ~128.</returns>
	protected byte Encode( float distance )
	{
		return (byte) ((int) ((distance * InvMaxDistance * 0.5f + 0.5f) * byte.MaxValue)).Clamp( 0, 255 );
	}

	/// <summary>
	/// Lazily creates / updates a texture containing the encoded samples for use in shaders.
	/// </summary>
	public Texture Texture
	{
		get
		{
			if ( !_textureInvalid && _texture != null ) return _texture;

			ThreadSafe.AssertIsMainThread();

			_textureInvalid = false;

			if ( _texture == null )
				_texture = CreateTexture();
			else
				UpdateTexture( _texture );

			return _texture;
		}
	}

	/// <summary>
	/// Implements creating a texture containing the encoded samples.
	/// </summary>
	/// <returns>A 2D / 3D texture containing the samples</returns>
	protected abstract Texture CreateTexture();

	/// <summary>
	/// Implements updating a texture containing the encoded samples.
	/// </summary>
	protected abstract void UpdateTexture( Texture texture );

	/// <summary>
	/// Implements adding a local-space shape to the samples in this array.
	/// </summary>
	/// <typeparam name="T">SDF type</typeparam>
	/// <param name="sdf">Shape to add</param>
	/// <returns>True if any geometry was modified</returns>
	public abstract Task<bool> AddAsync<T>( T sdf )
		where T : TSdf;

	/// <summary>
	/// Implements subtracting a local-space shape from the samples in this array.
	/// </summary>
	/// <typeparam name="T">SDF type</typeparam>
	/// <param name="sdf">Shape to subtract</param>
	/// <returns>True if any geometry was modified</returns>
	public abstract Task<bool> SubtractAsync<T>( T sdf )
		where T : TSdf;

	public abstract Task<bool> RebuildAsync( IEnumerable<ChunkModification<TSdf>> modifications );

	internal void Init( WorldQuality quality )
	{
		if ( Quality.Equals( quality ) )
		{
			return;
		}

		Quality = quality;

		ArraySize = Quality.ChunkResolution + Margin * 2 + 1;
		UnitSize = Quality.ChunkSize / Quality.ChunkResolution;
		InvUnitSize = Quality.ChunkResolution / Quality.ChunkSize;
		InvMaxDistance = 1f / Quality.MaxDistance;

		SampleCount = 1;

		for ( var i = 0; i < Dimensions; ++i )
		{
			SampleCount *= ArraySize;
		}

		BackBuffer = new byte[SampleCount];
		FrontBuffer = new byte[SampleCount];

		Clear( false );
	}

	/// <summary>
	/// Sets every sample to solid or empty.
	/// </summary>
	/// <param name="solid">Solidity to set each sample to.</param>
	public void Clear( bool solid )
	{
		Array.Fill( BackBuffer, solid ? (byte) 0 : (byte) 255 );
		SwapBuffers();
		MarkChanged();
	}

	/// <summary>
	/// Invalidates the texture, and collision / render meshes for the chunk this array represents.
	/// This doesn't trigger a net write.
	/// </summary>
	protected void MarkChanged()
	{
		_textureInvalid = true;
	}

	/// <inhertidoc />
	public void Dispose()
	{
		_texture?.Dispose();
		_texture = null;
	}
}
