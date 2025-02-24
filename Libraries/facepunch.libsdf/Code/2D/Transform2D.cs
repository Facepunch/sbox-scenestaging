using System;

namespace Sandbox.Sdf;

/// <summary>
/// Represents a 2D rotation around the Z axis.
/// </summary>
/// <param name="Cos">Cosine of the rotation angle.</param>
/// <param name="Sin">Sine of the rotation angle.</param>
public record struct Rotation2D( float Cos, float Sin )
{
	/// <summary>
	/// Converts an angle in degrees to a <see cref="Rotation2D"/>.
	/// </summary>
	/// <param name="degrees">Angle in degrees.</param>
	/// <returns>Rotation corresponding to the given angle.</returns>
	public static implicit operator Rotation2D( float degrees )
	{
		return new Rotation2D( degrees );
	}

	/// <summary>
	/// Converts a rotation to its vector representation.
	/// </summary>
	public static implicit operator Vector2( Rotation2D rotation )
	{
		return new Vector2( rotation.Cos, rotation.Sin );
	}

	/// <summary>
	/// Converts a rotation from its vector representation.
	/// </summary>
	public static implicit operator Rotation2D( Vector2 vector )
	{
		return new Rotation2D( vector.x, vector.y );
	}

	/// <summary>
	/// Represents a rotation of 0 degrees.
	/// </summary>
	public static Rotation2D Identity { get; } = new( 1f, 0f );

	/// <summary>
	/// Applies a rotation to a vector.
	/// </summary>
	/// <param name="rotation">Rotation to apply.</param>
	/// <param name="vector">Vector to rotate.</param>
	/// <returns>A rotated vector.</returns>
	public static Vector2 operator *( Rotation2D rotation, Vector2 vector )
	{
		return rotation.UnitX * vector.x + rotation.UnitY * vector.y;
	}

	/// <summary>
	/// Combines two rotations.
	/// </summary>
	/// <param name="lhs">First rotation.</param>
	/// <param name="rhs">Second rotation.</param>
	/// <returns>A combined rotation.</returns>
	public static Rotation2D operator *( Rotation2D lhs, Rotation2D rhs )
	{
		return new Rotation2D( lhs.Cos * rhs.Cos - lhs.Sin * rhs.Sin, lhs.Sin * rhs.Cos + lhs.Cos * rhs.Sin );
	}

	/// <summary>
	/// Result of rotating (1, 0) by this rotation.
	/// </summary>
	public Vector2 UnitX => new( Cos, -Sin );

	/// <summary>
	/// Result of rotating (0, 1) by this rotation.
	/// </summary>
	public Vector2 UnitY => new( Sin, Cos );

	/// <summary>
	/// Inverse of this rotation.
	/// </summary>
	public Rotation2D Inverse => this with { Sin = -Sin };

	/// <summary>
	/// A normalized version of this rotation, in case <see cref="Cos"/>^2 + <see cref="Sin"/>^2 != 1.
	/// </summary>
	public Rotation2D Normalized
	{
		get
		{
			var length = MathF.Sqrt( Cos * Cos + Sin * Sin );
			var scale = 1f / length;

			return new Rotation2D( Cos * scale, Sin * scale );
		}
	}

	/// <summary>
	/// Represents a 2D rotation around the Z axis.
	/// </summary>
	/// <param name="degrees">Angle in degrees.</param>
	public Rotation2D( float degrees )
		: this( MathF.Cos( degrees * MathF.PI / 180f ), MathF.Sin( degrees * MathF.PI / 180f ) )
	{
	}
}

/// <inheritdoc cref="Transform" />
/// <param name="Position">Translation this transform will apply.</param>
/// <param name="Rotation">Rotation this transform will apply.</param>
/// <param name="Scale">Scale this transform will apply.</param>
/// <param name="InverseScale">Inverse of <see cref="Scale"/>.</param>
public record struct Transform2D( Vector2 Position, Rotation2D Rotation, float Scale, float InverseScale )
{
	/// <summary>
	/// Represents no transformation.
	/// </summary>
	public static Transform2D Identity { get; } = new( Vector2.Zero, Rotation2D.Identity );

	/// <inheritdoc cref="Transform" />
	/// <param name="position">Translation this transform will apply.</param>
	/// <param name="rotation">Rotation this transform will apply.</param>
	/// <param name="scale">Scale this transform will apply.</param>
	public Transform2D( Vector2? position = null, Rotation2D? rotation = null, float scale = 1f )
		: this( position ?? Vector2.Zero, rotation ?? Rotation2D.Identity, scale, 1f / scale )
	{
	}

	/// <summary>
	/// Apply this transformation to a position.
	/// </summary>
	/// <param name="pos">Position to transform.</param>
	/// <returns>Transformed position.</returns>
	public Vector2 TransformPoint( Vector2 pos )
	{
		return Position + Rotation * (pos * Scale);
	}

	/// <summary>
	/// Apply the inverse of this transformation to a position.
	/// </summary>
	/// <param name="pos">Position to transform.</param>
	/// <returns>Transformed position.</returns>
	public Vector2 InverseTransformPoint( Vector2 pos )
	{
		return InverseScale * (Rotation.Inverse * (pos - Position));
	}
}
