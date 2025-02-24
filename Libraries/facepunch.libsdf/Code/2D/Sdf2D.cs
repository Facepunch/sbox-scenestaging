using System;
using System.Collections.Generic;
using System.IO;

namespace Sandbox.Sdf;

/// <summary>
/// Base interface for shapes that can be added to or subtracted from a <see cref="Sdf2DWorld"/>.
/// </summary>
public interface ISdf2D : ISdf<ISdf2D>
{
	/// <summary>
	/// Axis aligned bounds that fully encloses the surface of this shape.
	/// </summary>
	Rect Bounds { get; }

	/// <summary>
	/// Find the signed distance of a point from the surface of this shape.
	/// Positive values are outside, negative are inside, and 0 is exactly on the surface.
	/// </summary>
	/// <param name="pos">Position to sample at</param>
	/// <returns>A signed distance from the surface of this shape</returns>
	float this[Vector2 pos] { get; }
}

/// <summary>
/// Some extension methods for <see cref="ISdf2D"/>.
/// </summary>
public static class Sdf2DExtensions
{
	[RegisterSdfTypes]
	private static void RegisterTypes()
	{
		ISdf2D.RegisterType( RectSdf.ReadRaw );
		ISdf2D.RegisterType( CircleSdf.ReadRaw );
		ISdf2D.RegisterType( LineSdf.ReadRaw );
		ISdf2D.RegisterType( TextureSdf.ReadRaw );
		ISdf2D.RegisterType( TransformedSdf2D<ISdf2D>.ReadRaw );
		ISdf2D.RegisterType( TranslatedSdf2D<ISdf2D>.ReadRaw );
		ISdf2D.RegisterType( ExpandedSdf2D<ISdf2D>.ReadRaw );
	}

	/// <summary>
	/// Moves the given SDF by the specified offset.
	/// </summary>
	/// <typeparam name="T">SDF type</typeparam>
	/// <param name="sdf">SDF to translate</param>
	/// <param name="offset">Offset to translate by</param>
	/// <returns>A translated version of <paramref name="sdf"/></returns>
	public static TranslatedSdf2D<T> Translate<T>( this T sdf, Vector2 offset )
		where T : ISdf2D
	{
		return new TranslatedSdf2D<T>( sdf, offset );
	}

	/// <summary>
	/// Scales, rotates, and translates the given SDF.
	/// </summary>
	/// <typeparam name="T">SDF type</typeparam>
	/// <param name="sdf">SDF to transform</param>
	/// <param name="transform">Transformation to apply</param>
	/// <returns>A transformed version of <paramref name="sdf"/></returns>
	public static TransformedSdf2D<T> Transform<T>( this T sdf, Transform2D transform )
		where T : ISdf2D
	{
		return new TransformedSdf2D<T>( sdf, transform );
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
	public static TransformedSdf2D<T> Transform<T>( this T sdf, Vector2? translation = null,
		Rotation2D? rotation = null, float scale = 1f )
		where T : ISdf2D
	{
		return new TransformedSdf2D<T>( sdf, new Transform2D( translation, rotation, scale ) );
	}

	/// <summary>
	/// Expands the surface of the given SDF by the specified margin.
	/// </summary>
	/// <typeparam name="T">SDF type</typeparam>
	/// <param name="sdf">SDF to expand</param>
	/// <param name="margin">Distance to expand by</param>
	/// <returns>An expanded version of <paramref name="sdf"/></returns>
	public static ExpandedSdf2D<T> Expand<T>( this T sdf, float margin )
		where T : ISdf2D
	{
		return new ExpandedSdf2D<T>( sdf, margin );
	}
}

/// <summary>
/// Describes an axis-aligned rectangle with rounded corners.
/// </summary>
/// <param name="Min">Position of the corner with smallest X and Y values</param>
/// <param name="Max">Position of the corner with largest X and Y values</param>
/// <param name="CornerRadius">Controls the roundness of corners, or 0 for (approximately) sharp corners</param>
public record struct RectSdf( Vector2 Min, Vector2 Max, float CornerRadius = 0f ) : ISdf2D
{
	/// <summary>
	/// Describes an axis-aligned rectangle with rounded corners.
	/// </summary>
	/// <param name="rect">Size and position of the box</param>
	/// <param name="cornerRadius">Controls the roundness of corners, or 0 for (approximately) sharp corners</param>
	public RectSdf( Rect rect, float cornerRadius = 0f )
		: this( rect.TopLeft, rect.BottomRight, cornerRadius )
	{
	}

	/// <inheritdoc />
	public Rect Bounds => new( Min, Max - Min );

	/// <inheritdoc />
	public float this[Vector2 pos]
	{
		get
		{
			var dist2 = Vector2.Max( Min + CornerRadius - pos, pos - Max + CornerRadius );

			return (dist2.x <= 0f || dist2.y <= 0f
				? Math.Max( dist2.x, dist2.y )
				: dist2.Length) - CornerRadius;
		}
	}

	public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
	{
		writer.Write( Min );
		writer.Write( Max );
		writer.Write( CornerRadius );
	}

	public static RectSdf ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf2D>> sdfTypes )
	{
		return new RectSdf(
			reader.Read<Vector2>(),
			reader.Read<Vector2>(),
			reader.Read<float>() );
	}
}

/// <summary>
/// Describes a circle with a position and radius.
/// </summary>
/// <param name="Center">Position of the center of the circle</param>
/// <param name="Radius">Distance from the center to the edge of the circle</param>
public record struct CircleSdf( Vector2 Center, float Radius ) : ISdf2D
{
	/// <inheritdoc />
	public Rect Bounds => new( Center - Radius, Radius * 2f );

	/// <inheritdoc />
	public float this[Vector2 pos] => (pos - Center).Length - Radius;

	public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
	{
		writer.Write( Center );
		writer.Write( Radius );
	}

	public static CircleSdf ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf2D>> sdfTypes )
	{
		return new CircleSdf(
			reader.Read<Vector2>(),
			reader.Read<float>() );
	}
}

/// <summary>
/// Describes a line between two points with rounded ends of a given radius.
/// </summary>
/// <param name="PointA">Start point of the line</param>
/// <param name="PointB">End point of the line</param>
/// <param name="Radius">Radius of the end caps, and half the width of the line</param>
/// <param name="Along">
/// Internal helper vector for optimization.
/// Please use the other constructor instead of specifying this yourself.
/// </param>
public record struct LineSdf( Vector2 PointA, Vector2 PointB, float Radius, Vector2 Along ) : ISdf2D
{
	/// <summary>
	/// Describes a line between two points with rounded ends of a given radius.
	/// </summary>
	/// <param name="pointA">Start point of the line</param>
	/// <param name="pointB">End point of the line</param>
	/// <param name="radius">Radius of the end caps, and half the width of the line</param>
	public LineSdf( Vector2 pointA, Vector2 pointB, float radius )
		: this( pointA, pointB, radius, pointA.AlmostEqual( pointB )
			? Vector2.Zero
			: (pointB - pointA) / (pointB - pointA).LengthSquared )
	{
	}

	/// <inheritdoc />
	public Rect Bounds
	{
		get
		{
			var min = Vector2.Min( PointA, PointB );
			var max = Vector2.Max( PointA, PointB );

			return new Rect( min - Radius, max - min + Radius * 2f );
		}
	}

	/// <inheritdoc />
	public float this[Vector2 pos]
	{
		get
		{
			var t = Vector2.Dot( pos - PointA, Along );
			var closest = Vector2.Lerp( PointA, PointB, t );

			return (pos - closest).Length - Radius;
		}
	}

	public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
	{
		writer.Write( PointA );
		writer.Write( PointB );
		writer.Write( Radius );
	}

	public static LineSdf ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf2D>> sdfTypes )
	{
		return new LineSdf(
			reader.Read<Vector2>(),
			reader.Read<Vector2>(),
			reader.Read<float>() );
	}
}

/// <summary>
/// RGBA pixel color channel.
/// </summary>
public enum ColorChannel
{
	/// <summary>
	/// Red component.
	/// </summary>
	R,

	/// <summary>
	/// Green component.
	/// </summary>
	G,

	/// <summary>
	/// Blue component.
	/// </summary>
	B,

	/// <summary>
	/// Alpha component.
	/// </summary>
	A
}

/// <summary>
/// A SDF loaded from a <see cref="Texture"/>.
/// </summary>
public readonly struct TextureSdf : ISdf2D
{
	private readonly Texture _texture;
	private readonly int _gradientWidthPixels;
	private readonly ColorChannel _channel;
	private readonly Vector2 _worldSize;
	private readonly Vector2 _worldOffset;
	private readonly Vector2 _invSampleSize;
	private readonly (int Width, int Height) _imageSize;
	private readonly float[] _samples;

	/// <summary>
	/// A SDF loaded from a color channel from a <see cref="Texture"/>.
	/// By default, bright values represent the exterior, and light values represent the interior.
	/// </summary>
	/// <param name="texture">Texture to read from</param>
	/// <param name="gradientWidthPixels">Distance, in pixels, between the lightest and darkest values of gradients in <paramref name="texture"/></param>
	/// <param name="worldWidth">The desired width of the resulting SDF. The height will be determined using <paramref name="texture"/>'s aspect ratio.</param>
	/// <param name="pivot">
	/// Relative position of the origin in this SDF. This is also the point that the SDF will rotate around.
	/// (0, 0) is the bottom-left, (1, 1) is the top-right, and therefore (0.5, 0.5) is the center (default).
	/// </param>
	/// <param name="channel">Color channel to read from</param>
	/// <param name="invert">If false (default), bright values are external. If true, bright values are internal.</param>
	public TextureSdf( Texture texture, int gradientWidthPixels, float worldWidth,
		Vector2? pivot = null, ColorChannel channel = ColorChannel.R, bool invert = false )
		: this(
			texture, 
			gradientWidthPixels * (invert ? -1 : 1),
			channel,
			new Vector2( worldWidth, worldWidth * texture.Height / texture.Width ),
			new Vector2( worldWidth, worldWidth * texture.Height / texture.Width ) * -(pivot ?? new Vector2( 0.5f, 0.5f )) )
	{

	}

	private TextureSdf( Texture texture, int gradientWidthPixels, ColorChannel channel, Vector2 worldSize, Vector2 worldOffset )
	{
		if ( texture.ResourceId == 0 )
		{
			throw new ArgumentException( "Texture must be a resource loaded from disc." );
		}

		_texture = texture;
		_gradientWidthPixels = gradientWidthPixels;
		_channel = channel;

		_worldSize = worldSize;
		_worldOffset = worldOffset;
		_imageSize = (texture.Width, texture.Height);
		_invSampleSize = new Vector2( _imageSize.Width, _imageSize.Height ) / _worldSize;

		var colors = texture.GetPixels();
		var scale = worldSize.x * gradientWidthPixels / texture.Width;

		_samples = new float[_imageSize.Width * _imageSize.Height];

		switch ( channel )
		{
			case ColorChannel.R:
				for ( var i = 0; i < colors.Length; ++i ) _samples[i] = scale * (colors[i].r / 255f - 0.5f);
				break;

			case ColorChannel.G:
				for ( var i = 0; i < colors.Length; ++i ) _samples[i] = scale * (colors[i].g / 255f - 0.5f);
				break;

			case ColorChannel.B:
				for ( var i = 0; i < colors.Length; ++i ) _samples[i] = scale * (colors[i].b / 255f - 0.5f);
				break;

			case ColorChannel.A:
				for ( var i = 0; i < colors.Length; ++i ) _samples[i] = scale * (colors[i].a / 255f - 0.5f);
				break;
		}
	}

	/// <inheritdoc />
	public Rect Bounds => new( _worldOffset, _worldSize );

	/// <inheritdoc />
	public float this[Vector2 pos]
	{
		get
		{
			var localPos = (pos - _worldOffset) * _invSampleSize;

			var x = (int)MathF.Round( localPos.x );
			var y = (int)MathF.Round( localPos.y );

			if ( x < 0 || y < 0 || x >= _imageSize.Width || y >= _imageSize.Height ) return float.PositiveInfinity;

			y = _imageSize.Height - y - 1;

			return _samples[x + y * _imageSize.Width];
		}
	}
	
	public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
	{
		writer.Write( _texture.ResourcePath );
		writer.Write( _gradientWidthPixels );
		writer.Write( (byte) _channel );
		writer.Write( _worldSize );
		writer.Write( _worldOffset );
	}

	public static TextureSdf ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf2D>> sdfTypes )
	{
		return new TextureSdf(
			Texture.Load( FileSystem.Mounted, reader.Read<string>() ),
			reader.Read<int>(),
			(ColorChannel) reader.Read<byte>(),
			reader.Read<Vector2>(),
			reader.Read<Vector2>() );
	}
}

/// <summary>
/// Helper struct returned by <see cref="Sdf2DExtensions.Transform{T}(T,Transform2D)"/>
/// </summary>
public record struct TransformedSdf2D<T>( T Sdf, Transform2D Transform, Rect Bounds ) : ISdf2D
	where T : ISdf2D
{
	private static Rect CalculateBounds( T sdf, Transform2D transform )
	{
		var inner = sdf.Bounds;

		var a = transform.TransformPoint( inner.TopLeft );
		var b = transform.TransformPoint( inner.TopRight );
		var c = transform.TransformPoint( inner.BottomLeft );
		var d = transform.TransformPoint( inner.BottomRight );

		var min = Vector2.Min( Vector2.Min( a, b ), Vector2.Min( c, d ) );
		var max = Vector2.Max( Vector2.Max( a, b ), Vector2.Max( c, d ) );

		return new Rect( min.x, min.y, max.x - min.x, max.y - min.y );
	}

	/// <summary>
	/// Helper struct returned by <see cref="Sdf2DExtensions.Transform{T}(T,Transform2D)"/>
	/// </summary>
	public TransformedSdf2D( T sdf, Transform2D transform )
		: this( sdf, transform, CalculateBounds( sdf, transform ) )
	{
	}

	/// <inheritdoc />
	public float this[Vector2 pos] => Sdf[Transform.InverseTransformPoint( pos )] * Transform.InverseScale;

	public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
	{
		Sdf.Write( ref writer, sdfTypes );
		writer.Write( Transform.Position );
		writer.Write( Transform.Rotation );
		writer.Write( Transform.Scale );
	}

	public static TransformedSdf2D<T> ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf2D>> sdfTypes )
	{
		return new TransformedSdf2D<T>(
			(T)ISdf2D.Read( ref reader, sdfTypes ),
			new Transform2D(
				reader.Read<Vector2>(),
				reader.Read<Vector2>(),
				reader.Read<float>() ) );
	}
}

/// <summary>
/// Helper struct returned by <see cref="Sdf2DExtensions.Translate{T}"/>
/// </summary>
public record struct TranslatedSdf2D<T>( T Sdf, Vector2 Offset ) : ISdf2D
	where T : ISdf2D
{
	/// <inheritdoc />
	public Rect Bounds => new( Sdf.Bounds.Position + Offset, Sdf.Bounds.Size );

	/// <inheritdoc />
	public float this[Vector2 pos] => Sdf[pos - Offset];

	public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
	{
		Sdf.Write( ref writer, sdfTypes );
		writer.Write( Offset );
	}

	public static TranslatedSdf2D<T> ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf2D>> sdfTypes )
	{
		return new TranslatedSdf2D<T>(
			(T) ISdf2D.Read( ref reader, sdfTypes ),
			reader.Read<Vector2>() );
	}
}

/// <summary>
/// Helper struct returned by <see cref="Sdf2DExtensions.Expand{T}"/>
/// </summary>
public record struct ExpandedSdf2D<T>( T Sdf, float Margin ) : ISdf2D
	where T : ISdf2D
{
	/// <inheritdoc />
	public Rect Bounds => Sdf.Bounds.Grow( Margin );

	/// <inheritdoc />
	public float this[Vector2 pos] => Sdf[pos] - Margin;

	public void WriteRaw( ref ByteStream writer, Dictionary<TypeDescription, int> sdfTypes )
	{
		Sdf.Write( ref writer, sdfTypes );
		writer.Write( Margin );
	}

	public static ExpandedSdf2D<T> ReadRaw( ref ByteStream reader, IReadOnlyDictionary<int, SdfReader<ISdf2D>> sdfTypes )
	{
		return new ExpandedSdf2D<T>(
			(T) ISdf2D.Read( ref reader, sdfTypes ),
			reader.Read<float>() );
	}
}
