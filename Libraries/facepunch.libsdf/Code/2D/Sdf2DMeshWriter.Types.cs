using System;
using System.Runtime.InteropServices;

namespace Sandbox.Sdf;

partial class Sdf2DMeshWriter
{
	/// <summary>
	/// <code>
	/// c - d
	/// |   |
	/// a - b
	/// </code>
	/// </summary>
	[Flags]
	private enum SquareConfiguration : byte
	{
		None = 0,

		A = 1,
		B = 2,
		C = 4,
		D = 8,

		AB = A | B,
		AC = A | C,
		BD = B | D,
		CD = C | D,

		AD = A | D,
		BC = B | C,

		ABC = A | B | C,
		ABD = A | B | D,
		ACD = A | C | D,
		BCD = B | C | D,

		ABCD = A | B | C | D
	}

	private enum NormalizedVertex : byte
	{
		A,
		AB,
		AC
	}

	private enum SquareVertex : byte
	{
		A,
		B,
		C,
		D,

		AB,
		AC,
		BD,
		CD
	}

	private record struct SourceEdge( VertexKey V0, VertexKey V1 )
	{
		public SourceEdge( int x, int y, SquareVertex V0, SquareVertex V1 )
			: this( VertexKey.Normalize( x, y, V0 ), VertexKey.Normalize( x, y, V1 ) )
		{
		}
	}

	private record struct VertexKey( int X, int Y, NormalizedVertex Vertex )
	{
		public static VertexKey Normalize( int x, int y, SquareVertex vertex )
		{
			switch ( vertex )
			{
				case SquareVertex.A:
					return new VertexKey( x, y, NormalizedVertex.A );

				case SquareVertex.AB:
					return new VertexKey( x, y, NormalizedVertex.AB );

				case SquareVertex.AC:
					return new VertexKey( x, y, NormalizedVertex.AC );


				case SquareVertex.B:
					return new VertexKey( x + 1, y, NormalizedVertex.A );

				case SquareVertex.C:
					return new VertexKey( x, y + 1, NormalizedVertex.A );

				case SquareVertex.D:
					return new VertexKey( x + 1, y + 1, NormalizedVertex.A );


				case SquareVertex.BD:
					return new VertexKey( x + 1, y, NormalizedVertex.AC );

				case SquareVertex.CD:
					return new VertexKey( x, y + 1, NormalizedVertex.AB );


				default:
					throw new NotImplementedException();
			}
		}
	}

	[StructLayout( LayoutKind.Sequential )]
	public record struct Vertex( Vector3 Position, Vector3 Normal, Vector4 Tangent, Vector2 TexCoord )
	{
		public static VertexAttribute[] Layout { get; } =
		{
			new( VertexAttributeType.Position, VertexAttributeFormat.Float32 ),
			new( VertexAttributeType.Normal, VertexAttributeFormat.Float32 ),
			new( VertexAttributeType.Tangent, VertexAttributeFormat.Float32, 4 ),
			new( VertexAttributeType.TexCoord, VertexAttributeFormat.Float32, 2 )
		};
	}

	public readonly struct VertexHelper : IVertexHelper<Vertex>
	{
		public Vector3 GetPosition( in Vertex vertex )
		{
			return vertex.Position;
		}

		private static Vector3 Slerp( Vector3 a, Vector3 b, float t )
		{
			var omega = Vector3.GetAngle( a, b ) * MathF.PI / 180f;

			if ( Math.Abs( omega ) <= 0.001f )
			{
				return Vector3.Lerp( a, b, t );
			}

			return (MathF.Sin( (1f - t) * omega ) * a + MathF.Sin( t * omega ) * b) / MathF.Sin( omega );
		}

		public Vertex Lerp( in Vertex a, in Vertex b, float t )
		{
			var normal = Slerp( a.Normal, b.Normal, t ).Normal;
			var tangent = Slerp( a.Tangent, b.Tangent, t );
			var binormal = Vector3.Cross( normal, tangent );

			tangent = Vector3.Cross( binormal, normal ).Normal;

			return new Vertex(
				Vector3.Lerp( a.Position, b.Position, t ),
				normal,
				new Vector4( tangent, 1f ),
				Vector2.Lerp( a.TexCoord, b.TexCoord, t ) );
		}
	}
}
