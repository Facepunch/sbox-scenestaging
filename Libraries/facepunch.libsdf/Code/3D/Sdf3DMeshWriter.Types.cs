using System;
using System.Runtime.InteropServices;

namespace Sandbox.Sdf;

partial class Sdf3DMeshWriter
{
	/// <summary>
	/// <code>
	/// Z = 0   Z = 1
	/// c - d   g - h
	/// |   |   |   |
	/// a - b   e - f
	/// </code>
	/// </summary>
	[Flags]
	private enum CubeConfiguration : byte
	{
		None = 0,

		A = 1,
		B = 2,
		C = 4,
		D = 8,
		E = 16,
		F = 32,
		G = 64,
		H = 128
	}

	private enum NormalizedVertex : byte
	{
		A,
		AB,
		AC,
		AE
	}

	private enum CubeVertex : byte
	{
		A = 0, B = 4, C = 8, D = 12,
		E = 16, F = 20, G = 24, H = 28,

		AB = A | NormalizedVertex.AB,
		AC = A | NormalizedVertex.AC,
		AE = A | NormalizedVertex.AE,
		BD = B | NormalizedVertex.AC,
		BF = B | NormalizedVertex.AE,
		CD = C | NormalizedVertex.AB,
		CG = C | NormalizedVertex.AE,
		DH = D | NormalizedVertex.AE,
		EF = E | NormalizedVertex.AB,
		EG = E | NormalizedVertex.AC,
		FH = F | NormalizedVertex.AC,
		GH = G | NormalizedVertex.AB,

		OffsetMask = 0x03,
		BaseMask = 0xff ^ OffsetMask
	}

	private enum UvPlane
	{
		NegX,
		PosX,
		NegY,
		PosY,
		NegZ,
		PosZ
	}

	private record struct VertexKey( int X, int Y, int Z, NormalizedVertex Vertex )
	{
		public static VertexKey Normalize( int x, int y, int z, CubeVertex vertex )
		{
			// ReSharper disable BitwiseOperatorOnEnumWithoutFlags
			var baseVertex = vertex & CubeVertex.BaseMask;
			var offset = (NormalizedVertex) (vertex & CubeVertex.OffsetMask);
			// ReSharper enable BitwiseOperatorOnEnumWithoutFlags

			switch ( baseVertex )
			{
				case CubeVertex.A:
					return new VertexKey( x + 0, y + 0, z + 0, offset );
				case CubeVertex.B:
					return new VertexKey( x + 1, y + 0, z + 0, offset );
				case CubeVertex.C:
					return new VertexKey( x + 0, y + 1, z + 0, offset );
				case CubeVertex.D:
					return new VertexKey( x + 1, y + 1, z + 0, offset );

				case CubeVertex.E:
					return new VertexKey( x + 0, y + 0, z + 1, offset );
				case CubeVertex.F:
					return new VertexKey( x + 1, y + 0, z + 1, offset );
				case CubeVertex.G:
					return new VertexKey( x + 0, y + 1, z + 1, offset );
				case CubeVertex.H:
					return new VertexKey( x + 1, y + 1, z + 1, offset );
				default:
					throw new NotImplementedException();
			}
		}
	}

	private record struct Triangle( VertexKey V0, VertexKey V1, VertexKey V2 )
	{
		public Triangle( int x, int y, int z, CubeVertex V0, CubeVertex V1, CubeVertex V2 )
			: this( VertexKey.Normalize( x, y, z, V0 ), VertexKey.Normalize( x, y, z, V1 ), VertexKey.Normalize( x, y, z, V2 ) )
		{
		}

		public Triangle Flipped => new( V0, V2, V1 );
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
}
