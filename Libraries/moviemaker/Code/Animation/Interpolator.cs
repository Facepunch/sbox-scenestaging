using System;

namespace Sandbox.Animation;

#nullable enable

internal interface IInterpolator<T>
{
	T Interpolate( T a, T b, float t );
}

internal sealed class DefaultInterpolator :
	IInterpolator<Half>, IInterpolator<float>, IInterpolator<double>,
	IInterpolator<Vector2>, IInterpolator<Vector3>, IInterpolator<Vector4>,
	IInterpolator<Rotation>, IInterpolator<Angles>,
	IInterpolator<Color>
{
	public static DefaultInterpolator Instance { get; } = new();

	public Half Interpolate( Half a, Half b, float t ) => Half.Lerp( a, b, (Half)t );
	public float Interpolate( float a, float b, float t ) => float.Lerp( a, b, t );
	public double Interpolate( double a, double b, float t ) => double.Lerp( a, b, t );
	public Vector2 Interpolate( Vector2 a, Vector2 b, float t ) => Vector2.Lerp( a, b, t );
	public Vector3 Interpolate( Vector3 a, Vector3 b, float t ) => Vector3.Lerp( a, b, t );
	public Vector4 Interpolate( Vector4 a, Vector4 b, float t ) => Vector4.Lerp( a, b, t );
	public Rotation Interpolate( Rotation a, Rotation b, float t ) => Rotation.Slerp( a, b, t );
	public Angles Interpolate( Angles a, Angles b, float t ) => Rotation.Slerp( a, b, t );
	public Color Interpolate( Color a, Color b, float t ) => Color.Lerp( a, b, t );
}
