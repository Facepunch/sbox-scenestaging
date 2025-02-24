using Editor.MovieMaker;

namespace Editor.TrackPainter;

#nullable enable

public abstract partial class CurvePreview<T> : BlockPreview<T>
{
	public record struct Element( string Name, Color Color, float? Min = null, float? Max = null );

	protected IReadOnlyList<Element> Elements { get; }

	protected CurvePreview( params Element[] elements )
	{
		Elements = elements;
	}

	protected abstract void Decompose( T value, Span<float> result );
}

#region Scalars

public sealed class BooleanPreview() : CurvePreview<bool>(
	new Element( "X", Color.White, 0f, 1f ) )
{
	protected override void Decompose( bool value, Span<float> result )
	{
		result[0] = value ? 1f : 0f;
	}
}

public sealed class FloatPreview() : CurvePreview<float>(
	new Element( "X", Color.White ) )
{
	protected override void Decompose( float value, Span<float> result )
	{
		result[0] = value;
	}
}

#endregion

#region Vectors

public sealed class Vector2Preview() : CurvePreview<Vector2>(
	new Element( "X", Theme.Red ),
	new Element( "Y", Theme.Green ) )
{
	protected override void Decompose( Vector2 value, Span<float> result )
	{
		result[0] = value.x;
		result[1] = value.y;
	}
}

public sealed class Vector3Preview() : CurvePreview<Vector3>(
	new Element( "X", Theme.Red ),
	new Element( "Y", Theme.Green ),
	new Element( "Z", Theme.Blue ) )
{
	protected override void Decompose( Vector3 value, Span<float> result )
	{
		result[0] = value.x;
		result[1] = value.y;
		result[2] = value.z;
	}
}

public sealed class Vector4Preview() : CurvePreview<Vector4>(
	new Element( "X", Theme.Red ),
	new Element( "Y", Theme.Green ),
	new Element( "Z", Theme.Blue ),
	new Element( "W", Theme.White ) )
{
	protected override void Decompose( Vector4 value, Span<float> result )
	{
		result[0] = value.x;
		result[1] = value.y;
		result[2] = value.z;
		result[3] = value.w;
	}
}

#endregion Vectors

#region Rotation

public sealed class AnglesPreview() : CurvePreview<Angles>(
	new Element( "P", Theme.Red, -180f, 180f ),
	new Element( "Y", Theme.Green, -180f, 180f ),
	new Element( "R", Theme.Blue, -180f, 180f ) )
{
	protected override void Decompose( Angles value, Span<float> result )
	{
		result[0] = value.pitch;
		result[1] = value.yaw;
		result[2] = value.roll;
	}
}

public sealed class RotationPreview() : CurvePreview<Rotation>(
	new Element( "X", Theme.Red, -1f, 1f ),
	new Element( "Y", Theme.Green, -1f, 1f ),
	new Element( "Z", Theme.Blue, -1f, 1f ),
	new Element( "W", Theme.White, -1f, 1f ) )
{
	protected override void Decompose( Rotation value, Span<float> result )
	{
		// Decompose it as the forward vector + how much the right vector is pointing up,
		// because that looks nice and smooth

		var forward = value.Forward;
		var right = value.Right;

		result[0] = forward.x;
		result[1] = forward.y;
		result[2] = forward.z;
		result[3] = right.z;
	}
}

#endregion

#region Color

public sealed class ColorPreview() : CurvePreview<Color>(
	new Element( "R", Color.Red, 0f, 1f ),
	new Element( "G", Color.Green, 0f, 1f ),
	new Element( "B", Color.Blue, 0f, 1f ),
	new Element( "A", Color.White, 0f, 1f ) )
{
	protected override void Decompose( Color value, Span<float> result )
	{
		result[0] = value.r;
		result[1] = value.g;
		result[2] = value.b;
		result[3] = value.a;
	}
}

#endregion
