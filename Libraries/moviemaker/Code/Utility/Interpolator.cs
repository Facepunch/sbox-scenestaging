
namespace Sandbox.MovieMaker;

#nullable enable

public interface IInterpolator<T>
{
	T Interpolate( T a, T b, float t );
}

public static class Interpolator
{
	public static IInterpolator<T>? GetDefault<T>()
	{
		// TODO: type library lookup?

		return DefaultInterpolator.Instance as IInterpolator<T>;
	}
}

file sealed class DefaultInterpolator :
	IInterpolator<float>, IInterpolator<Vector2>, IInterpolator<Vector3>, IInterpolator<Vector4>,
	IInterpolator<Rotation>, IInterpolator<Angles>,
	IInterpolator<Color>
{
	public static DefaultInterpolator Instance { get; } = new();

	public float Interpolate( float a, float b, float delta ) => a + (b - a) * delta;
	public Vector2 Interpolate( Vector2 a, Vector2 b, float delta ) => a + (b - a) * delta;
	public Vector3 Interpolate( Vector3 a, Vector3 b, float delta ) => a + (b - a) * delta;
	public Vector4 Interpolate( Vector4 a, Vector4 b, float delta ) => a + (b - a) * delta;
	public Rotation Interpolate( Rotation a, Rotation b, float delta ) => Rotation.Slerp( a, b, delta );
	public Angles Interpolate( Angles a, Angles b, float delta ) => Rotation.Slerp( a, b, delta );
	public Color Interpolate( Color a, Color b, float delta ) => a + (b - a) * delta;
}
