using System;
using System.Linq;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Sandbox.Sdf.Noise;
using Sandbox.UI;
using Sandbox.Utility;

namespace Sandbox.Sdf
{
	/// <summary>
	/// Base interface for shapes that can be added to or subtracted from a <see cref="Sdf3DWorld"/>.
	/// </summary>
	public interface ISdf3D : ISdf<ISdf3D>
	{
		/// <summary>
		/// Axis aligned bounds that fully encloses the surface of this shape.
		/// </summary>
		BBox? Bounds { get; }

		/// <summary>
		/// Find the signed distance of a point from the surface of this shape.
		/// Positive values are outside, negative are inside, and 0 is exactly on the surface.
		/// </summary>
		/// <param name="pos">Position to sample at</param>
		/// <returns>A signed distance from the surface of this shape</returns>
		float this[Vector3 pos] { get; }

		/// <summary>
		/// Sample an axis-aligned box shaped region, writing to an <paramref name="output"/> array.
		/// </summary>
		/// <param name="transform">Transformation to apply to the SDF.</param>
		/// <param name="output">Array to write signed distance values to.</param>
		/// <param name="outputSize">Dimensions of the <paramref name="output"/> array.</param>
		public async Task SampleRangeAsync( Transform transform, float[] output, (int X, int Y, int Z) outputSize )
		{
			await GameTask.WorkerThread();

			for ( var z = 0; z < outputSize.Z; ++z )
			{
				for ( var y = 0; y < outputSize.Y; ++y )
				{
					for ( int x = 0, index = (y + z * outputSize.Y) * outputSize.X; x < outputSize.X; ++x, ++index )
					{
						output[index] = this[transform.PointToWorld( new Vector3( x, y, z ) )];
					}
				}
			}
		}
	}

	/// <summary>
	/// Some extension methods for <see cref="ISdf3D"/>.
	/// </summary>
	public static class Sdf3DExtensions
	{
		[RegisterSdfTypes]
		private static void RegisterTypes()
		{
			ISdf3D.RegisterType( BoxSdf3D.ReadRaw );
			ISdf3D.RegisterType( SphereSdf3D.ReadRaw );
			ISdf3D.RegisterType( CapsuleSdf3D.ReadRaw );
			ISdf3D.RegisterType( TransformedSdf3D<ISdf3D>.ReadRaw );
			ISdf3D.RegisterType( TranslatedSdf3D<ISdf3D>.ReadRaw );
			ISdf3D.RegisterType( ExpandedSdf3D<ISdf3D>.ReadRaw );
			ISdf3D.RegisterType( IntersectedSdf3D<ISdf3D, ISdf3D>.ReadRaw );
			ISdf3D.RegisterType( BiasedSdf3D<ISdf3D, ISdf3D>.ReadRaw );
			ISdf3D.RegisterType( CellularNoiseSdf3D.ReadRaw );
			ISdf3D.RegisterType( HeightmapSdf3D.ReadRaw );
			ISdf3D.RegisterType( NoiseSdf3D.ReadRaw );
		}

		/// <summary>
		/// Moves the given SDF by the specified offset.
		/// </summary>
		/// <typeparam name="T">SDF type</typeparam>
		/// <param name="sdf">SDF to translate</param>
		/// <param name="offset">Offset to translate by</param>
		/// <returns>A translated version of <paramref name="sdf"/></returns>
		public static TranslatedSdf3D<T> Translate<T>( this T sdf, Vector3 offset )
			where T : ISdf3D
		{
			return new TranslatedSdf3D<T>( sdf, offset );
		}

		/// <summary>
		/// Scales, rotates, and translates the given SDF.
		/// </summary>
		/// <typeparam name="T">SDF type</typeparam>
		/// <param name="sdf">SDF to transform</param>
		/// <param name="transform">Transformation to apply</param>
		/// <returns>A transformed version of <paramref name="sdf"/></returns>
		public static TransformedSdf3D<T> Transform<T>( this T sdf, Transform transform )
			where T : ISdf3D
		{
			return new TransformedSdf3D<T>( sdf, transform );
		}

		/// <summary>
		/// Scales, rotates, and translates the given SDF.
		/// </summary>
		/// <typeparam name="T">SDF type</typeparam>
		/// <param name="sdf">SDF to transform</param>
		/// <param name="translation">Offset to translate by</param>
		/// <param name="rotation">Rotation to apply</param>
		/// <param name="scale">Scale multiplier to apply</param>
		/// <returns>A transformed version of <paramref name="sdf"/></returns>
		public static TransformedSdf3D<T> Transform<T>( this T sdf, Vector3? translation = null, Rotation? rotation = null, float scale = 1f )
			where T : ISdf3D
		{
			return new TransformedSdf3D<T>( sdf, new Transform( translation ?? Vector3.Zero, rotation ?? Rotation.Identity, scale ) );
		}

		/// <summary>
		/// Expands the surface of the given SDF by the specified margin.
		/// </summary>
		/// <typeparam name="T">SDF type</typeparam>
		/// <param name="sdf">SDF to expand</param>
		/// <param name="margin">Distance to expand by</param>
		/// <returns>An expanded version of <paramref name="sdf"/></returns>
		public static ExpandedSdf3D<T> Expand<T>( this T sdf, float margin )
			where T : ISdf3D
		{
			return new ExpandedSdf3D<T>( sdf, margin );
		}

		public static IntersectedSdf3D<T1, T2> Intersection<T1, T2>( this T1 sdf1, T2 sdf2 )
			where T1 : ISdf3D
			where T2 : ISdf3D
		{
			return new IntersectedSdf3D<T1, T2>( sdf1, sdf2 );
		}

		public static BiasedSdf3D<T, TBias> Bias<T, TBias>( this T sdf, TBias biasSdf, float biasScale = 1f )
			where T : ISdf3D
			where TBias : ISdf3D
		{
			return new BiasedSdf3D<T, TBias>( sdf, biasSdf, biasScale );
		}
	}

	/// <summary>
	/// Describes an axis-aligned box with rounded corners.
	/// </summary>
	/// <param name="Min">Position of the corner with smallest X, Y and Z values</param>
	/// <param name="Max">Position of the corner with largest X, Y and Z values</param>
	/// <param name="CornerRadius">Controls the roundness of corners, or 0 for (approximately) sharp corners</param>
	public record struct BoxSdf3D( Vector3 Min, Vector3 Max, float CornerRadius = 0f ) : ISdf3D
	{
		/// <summary>
		/// Describes an axis-aligned box with rounded corners.
		/// </summary>
		/// <param name="box">Size and position of the box</param>
		/// <param name="cornerRadius">Controls the roundness of corners, or 0 for (approximately) sharp corners</param>
		public BoxSdf3D( BBox box, float cornerRadius = 0f )
			: this( box.Mins, box.Maxs, cornerRadius )
		{

		}

		/// <inheritdoc />
		public BBox? Bounds => new( Min, Max );

		/// <inheritdoc />
		public float this[Vector3 pos]
		{
			get
			{
				var dist3 = Vector3.Max( Min + CornerRadius - pos, pos - Max + CornerRadius );

				return (dist3.x <= 0f && dist3.y <= 0f && dist3.z <= 0f
					? Math.Max( dist3.x, Math.Max( dist3.y, dist3.z ) )
					: Vector3.Max( dist3, 0f ).Length) - CornerRadius;
			}
		}

		public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
		{
			writer.Write( Min );
			writer.Write( Max );
			writer.Write( CornerRadius );
		}

		public static BoxSdf3D ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf3D>> sdfTypes )
		{
			return new BoxSdf3D(
				reader.Read<Vector3>(),
				reader.Read<Vector3>(), 
				reader.Read<float>() );
		}
	}

	/// <summary>
	/// Describes a sphere with a position and radius.
	/// </summary>
	/// <param name="Center">Position of the center of the sphere</param>
	/// <param name="Radius">Distance from the center to the surface of the sphere</param>
	public record struct SphereSdf3D( Vector3 Center, float Radius ) : ISdf3D
	{
		/// <inheritdoc />
		public BBox? Bounds => BBox.FromPositionAndSize( Center, Radius * 2f );

		/// <inheritdoc />
		public float this[Vector3 pos] => (pos - Center).Length - Radius;

		public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
		{
			writer.Write( Center );
			writer.Write( Radius );
		}

		public static SphereSdf3D ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf3D>> sdfTypes )
		{
			return new SphereSdf3D(
				reader.Read<Vector3>(),
				reader.Read<float>() );
		}
	}

	/// <summary>
	/// Describes two spheres connected by a cylinder, all with a common radius.
	/// </summary>
	/// <param name="PointA">Center of the first sphere</param>
	/// <param name="PointB">Center of the second sphere</param>
	/// <param name="Radius">Radius of the spheres and connecting cylinder</param>
	/// <param name="Along">
	/// Internal helper vector for optimization.
	/// Please use the other constructor instead of specifying this yourself.
	/// </param>
	public record struct CapsuleSdf3D( Vector3 PointA, Vector3 PointB, float Radius, Vector3 Along ) : ISdf3D
	{
		/// <summary>
		/// Describes two spheres connected by a cylinder, all with a common radius.
		/// </summary>
		/// <param name="pointA">Center of the first sphere</param>
		/// <param name="pointB">Center of the second sphere</param>
		/// <param name="radius">Radius of the spheres and connecting cylinder</param>
		public CapsuleSdf3D( Vector3 pointA, Vector3 pointB, float radius )
			: this( pointA, pointB, radius, pointA.AlmostEqual( pointB )
				? Vector3.Zero
				: (pointB - pointA) / (pointB - pointA).LengthSquared )
		{

		}

		/// <inheritdoc />
		public BBox? Bounds
		{
			get
			{
				var min = Vector3.Min( PointA, PointB );
				var max = Vector3.Max( PointA, PointB );

				return new BBox( min - Radius, max + Radius );
			}
		}

		/// <inheritdoc />
		public float this[Vector3 pos]
		{
			get
			{
				var t = Vector3.Dot( pos - PointA, Along );
				var closest = Vector3.Lerp( PointA, PointB, t );

				return (pos - closest).Length - Radius;
			}
		}

		public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
		{
			writer.Write( PointA );
			writer.Write( PointB );
			writer.Write( Radius );
		}

		public static CapsuleSdf3D ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf3D>> sdfTypes )
		{
			return new CapsuleSdf3D(
				reader.Read<Vector3>(),
				reader.Read<Vector3>(),
				reader.Read<float>() );
		}
	}

	/// <summary>
	/// Helper struct returned by <see cref="Transform"/>
	/// </summary>
	public record struct TransformedSdf3D<T>( T Sdf, Transform Transform, BBox? Bounds, float InverseScale ) : ISdf3D
		where T : ISdf3D
	{
		/// <summary>
		/// Helper struct returned by <see cref="Transform"/>
		/// </summary>
		public TransformedSdf3D( T sdf, Transform transform )
			: this( sdf, transform.WithScale( transform.UniformScale ), sdf.Bounds?.Transform( transform.WithScale( transform.UniformScale ) ), 1f / transform.UniformScale )
		{

		}

		/// <inheritdoc />
		public float this[Vector3 pos] => Sdf[Transform.PointToLocal( pos )] * InverseScale;

		Task ISdf3D.SampleRangeAsync( Transform transform, float[] output, (int X, int Y, int Z) outputSize )
		{
			return Sdf.SampleRangeAsync( Transform.ToLocal( transform ), output, outputSize );
		}

		public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
		{
			Sdf.Write( ref writer, sdfTypes );
			writer.Write( Transform.Position );
			writer.Write( Transform.Rotation );
			writer.Write( Transform.UniformScale );
		}

		public static TransformedSdf3D<T> ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf3D>> sdfTypes )
		{
			return new TransformedSdf3D<T>( (T) ISdf3D.Read( ref reader, sdfTypes ),
				new Transform(
					reader.Read<Vector3>(),
					reader.Read<Rotation>(),
					reader.Read<float>() ) );
		}
	}

	/// <summary>
	/// Helper struct returned by <see cref="Sdf3DExtensions.Translate{T}"/>
	/// </summary>
	public record struct TranslatedSdf3D<T>( T Sdf, Vector3 Offset ) : ISdf3D
		where T : ISdf3D
	{
		/// <inheritdoc />
		public BBox? Bounds => Sdf.Bounds?.Translate( Offset );

		/// <inheritdoc />
		public float this[Vector3 pos] => Sdf[pos - Offset];

		Task ISdf3D.SampleRangeAsync( Transform transform, float[] output, (int X, int Y, int Z) outputSize )
		{
			return Sdf.SampleRangeAsync( new Transform( Offset ).ToLocal( transform ), output, outputSize );
		}

		public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
		{
			Sdf.Write( ref writer, sdfTypes );
			writer.Write( Offset );
		}

		public static TranslatedSdf3D<T> ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf3D>> sdfTypes )
		{
			return new TranslatedSdf3D<T>( (T) ISdf3D.Read( ref reader, sdfTypes ), reader.Read<Vector3>() );
		}
	}

	/// <summary>
	/// Helper struct returned by <see cref="Sdf3DExtensions.Expand{T}"/>
	/// </summary>
	public record struct ExpandedSdf3D<T>( T Sdf, float Margin ) : ISdf3D
		where T : ISdf3D
	{
		/// <inheritdoc />
		public BBox? Bounds => Sdf.Bounds is { } bounds ? new( bounds.Mins - Margin, bounds.Maxs + Margin ) : null;

		/// <inheritdoc />
		public float this[Vector3 pos] => Sdf[pos] - Margin;

		async Task ISdf3D.SampleRangeAsync( Transform transform, float[] output, (int X, int Y, int Z) outputSize )
		{
			await Sdf.SampleRangeAsync( transform, output, outputSize );

			var sampleCount = outputSize.X * outputSize.Y * outputSize.Z;

			for ( var i = 0; i < sampleCount; ++i )
			{
				output[i] -= Margin;
			}
		}

		public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
		{
			Sdf.Write( ref writer, sdfTypes );
			writer.Write( Margin );
		}

		public static ExpandedSdf3D<T> ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf3D>> sdfTypes )
		{
			return new ExpandedSdf3D<T>( (T) ISdf3D.Read( ref reader, sdfTypes ), reader.Read<float>() );
		}
	}

	public record struct IntersectedSdf3D<T1, T2>( T1 Sdf1, T2 Sdf2, BBox? Bounds ) : ISdf3D
		where T1 : ISdf3D
		where T2 : ISdf3D
	{
		/// <inheritdoc />
		public float this[ Vector3 pos ] => Math.Max( Sdf1[pos], Sdf2[pos] );

		public IntersectedSdf3D( T1 sdf1, T2 sdf2 )
			: this( sdf1, sdf2,
				sdf1.Bounds is { } bounds1 && sdf2.Bounds is { } bounds2
					? new BBox( Vector3.Max( bounds1.Mins, bounds2.Mins ), Vector3.Min( bounds1.Maxs, bounds2.Maxs ) )
					: sdf1.Bounds ?? sdf2.Bounds )
		{

		}

		async Task ISdf3D.SampleRangeAsync( Transform transform, float[] output, (int X, int Y, int Z) outputSize )
		{
			var sampleCount = outputSize.X * outputSize.Y * outputSize.Z;
			var temp = ArrayPool<float>.Shared.Rent( sampleCount );

			try
			{
				await GameTask.WhenAll(
					Sdf1.SampleRangeAsync( transform, output, outputSize ),
					Sdf2.SampleRangeAsync( transform, temp, outputSize ) );

				for ( var i = 0; i < sampleCount; ++i )
				{
					output[i] = Math.Max( output[i], temp[i] );
				}
			}
			finally
			{
				ArrayPool<float>.Shared.Return( temp );
			}
		}

		public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
		{
			Sdf1.Write( ref writer, sdfTypes );
			Sdf2.Write( ref writer, sdfTypes );
		}

		public static IntersectedSdf3D<T1, T2> ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf3D>> sdfTypes )
		{
			return new IntersectedSdf3D<T1, T2>( (T1) ISdf3D.Read( ref reader, sdfTypes ), (T2) ISdf3D.Read( ref reader, sdfTypes ) );
		}
	}

	public record struct BiasedSdf3D<T, TBias>( T Sdf, TBias BiasSdf, float BiasScale ) : ISdf3D
		where T : ISdf3D
		where TBias : ISdf3D
	{
		/// <inheritdoc />
		public BBox? Bounds => Sdf.Bounds;

		/// <inheritdoc />
		public float this[Vector3 pos] => Sdf[pos] + BiasSdf[pos] * BiasScale;

		async Task ISdf3D.SampleRangeAsync( Transform transform, float[] output, (int X, int Y, int Z) outputSize )
		{
			if ( BiasScale == 0f )
			{
				await Sdf.SampleRangeAsync( transform, output, outputSize );
				return;
			}

			var sampleCount = outputSize.X * outputSize.Y * outputSize.Z;
			var temp = ArrayPool<float>.Shared.Rent( sampleCount );

			try
			{
				await GameTask.WhenAll(
					Sdf.SampleRangeAsync( transform, output, outputSize ),
					BiasSdf.SampleRangeAsync( transform, temp, outputSize ) );

				for ( var i = 0; i < sampleCount; ++i )
				{
					output[i] += temp[i] * BiasScale;
				}
			}
			finally
			{
				ArrayPool<float>.Shared.Return( temp );
			}
		}

		public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
		{
			Sdf.Write( ref writer, sdfTypes );
			BiasSdf.Write( ref writer, sdfTypes );
			writer.Write( BiasScale );
		}

		public static BiasedSdf3D<T, TBias> ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf3D>> sdfTypes )
		{
			return new BiasedSdf3D<T, TBias>( (T) ISdf3D.Read( ref reader, sdfTypes ), (TBias) ISdf3D.Read( ref reader, sdfTypes ), reader.Read<float>() );
		}
	}

	public record struct HeightmapSdf3D( INoiseField Noise, BBox? Bounds ) : ISdf3D
	{
		public HeightmapSdf3D( INoiseField noise, int resolution, float size )
			: this( noise, new BBox( 0f, new Vector3( size, size, FindMaxHeight( noise, resolution, size ) ) ) )
		{

		}

		private static float FindMaxHeight( INoiseField noise, int resolution, float size )
		{
			var max = 0f;
			var scale = size / (resolution - 1);

			for ( var x = 0; x < resolution; ++x )
			for ( var y = 0; y < resolution; ++y )
			{
				var worldPos = new Vector2( x, y ) * scale;
				var sample = noise.Sample( worldPos );

				max = Math.Max( max, sample );
			}

			return max;
		}

		public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
		{
			throw new NotImplementedException();
		}

		public static HeightmapSdf3D ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf3D>> sdfTypes )
		{
			throw new NotImplementedException();
		}

		public float this[ Vector3 pos ] => throw new NotImplementedException();

		[field: ThreadStatic] private static float[] _heightmapSamples;

		async Task ISdf3D.SampleRangeAsync( Transform transform, float[] output, (int X, int Y, int Z) outputSize )
		{
			if ( Vector3.Dot( transform.Rotation.Up, Vector3.Up ) < 0.9999f ) throw new NotImplementedException();

			await GameTask.WorkerThread();

			var hScale = transform.Scale.z;

			var hStride = outputSize.X + 2;
			var hSampleCount = hStride * (outputSize.Y + 2);

			if ( _heightmapSamples is null || _heightmapSamples.Length < hSampleCount )
			{
				Array.Resize( ref _heightmapSamples, hSampleCount );
			}

			var hSamples = _heightmapSamples;

			for ( var x = 0; x < outputSize.X + 2; ++x )
			for ( var y = 0; y < outputSize.Y + 2; ++y )
			{
				var worldPos = transform.PointToWorld( new Vector3( x - 1, y - 1 ) );
				hSamples[x + y * hStride] = (Noise.Sample( worldPos.x, worldPos.y ) - worldPos.z) / hScale;
			}

			for ( var x = 0; x < outputSize.X; ++x )
			for ( var y = 0; y < outputSize.Y; ++y )
			{
				var hIndex = x + 1 + (y + 1) * hStride;
				var sample = hSamples[hIndex];

				// Sample neighbouring points too, for a bit more accuracy on steep slopes

				var xNeg = hSamples[hIndex - 1];
				var xPos = hSamples[hIndex + 1];
				var yNeg = hSamples[hIndex - hStride];
				var yPos = hSamples[hIndex + hStride];

				// Find the highest / lowest neighbors, relative to center sample

				var max = Math.Max( 0, Math.Max( Math.Max( xNeg, xPos ), Math.Max( yNeg, yPos ) ) - sample );
				var min = Math.Min( 0, Math.Min( Math.Min( xNeg, xPos ), Math.Min( yNeg, yPos ) ) - sample );

				// Find out how much distance from line to neighbor increases with height,
				// both for above the surface and below

				var posInc = 1f / MathF.Sqrt( 1f + max * max );
				var negInc = 1f / MathF.Sqrt( 1f + min * min );

				for ( int z = 0, index = y * outputSize.X + x; z < outputSize.Z; ++z, index += outputSize.X * outputSize.Y )
				{
					output[index] = (z - sample) * hScale * (z > sample ? posInc : negInc);
				}
			}
		}
	}

	public record struct NoiseSdf3D( INoiseField Noise, float Threshold = 0.5f, float DistanceScale = 256f ) : ISdf3D
	{
		public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
		{
			throw new NotImplementedException();
		}
		public static NoiseSdf3D ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf3D>> sdfTypes )
		{
			throw new NotImplementedException();
		}

		public BBox? Bounds => null;

		public float this[ Vector3 pos ] => (Noise.Sample( pos ) - Threshold) * DistanceScale;
	}
}
