using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Sandbox.TerrainEngine;

public static class GeometryClipmap
{
	[StructLayout( LayoutKind.Sequential )]
	public struct PosAndLodVertex
	{
		public PosAndLodVertex( Vector3 position )
		{
			this.position = position;
		}

		public Vector3 position;

		public static readonly VertexAttribute[] Layout =
		{
			new VertexAttribute( VertexAttributeType.Position, VertexAttributeFormat.Float32, 3 ),
		};
	}

	public static Mesh GenerateMesh( int LodLevels, int LodExtentTexels, Material material )
	{
		// FIXME: Use our own vertex layout, we only need a position
		var vertices = new List<PosAndLodVertex>( 32 );
		var indices = new List<int>();

		// Loop through each LOD level
		for ( int level = 0; level < LodLevels; level++ )
		{
			int step = 1 << level;
			int prevStep = Math.Max( 0, 1 << (level - 1) );

			int g = LodExtentTexels / 2;

			int pad = 1;
			int radius = step * (g + pad);

			for ( int y = -radius; y < radius; y += step )
			{
				for ( int x = -radius; x < radius; x += step )
				{
					if ( Math.Max( Math.Abs( x + prevStep ), Math.Abs( y + prevStep ) ) < (g * prevStep) )
						continue;

					vertices.Add( new PosAndLodVertex( new Vector3( x, y, level ) ) );
					vertices.Add( new PosAndLodVertex( new Vector3( x + step, y, level ) ) );
					vertices.Add( new PosAndLodVertex( new Vector3( x + step, y + step, level ) ) );
					vertices.Add( new PosAndLodVertex( new Vector3( x, y + step, level ) ) );

					indices.Add( vertices.Count - 4 );
					indices.Add( vertices.Count - 3 );
					indices.Add( vertices.Count - 2 );
					indices.Add( vertices.Count - 2 );
					indices.Add( vertices.Count - 1 );
					indices.Add( vertices.Count - 4 );
				}
			}
		}

		var mesh = new Mesh( material );
		mesh.CreateVertexBuffer( vertices.Count, PosAndLodVertex.Layout, vertices );
		mesh.CreateIndexBuffer( indices.Count, indices );
		return mesh;
	}

	/// <summary>
	/// Inefficient implementation of diamond square, it's not merging verticies.
	/// </summary>
	/// <returns></returns>
	public static Mesh GenerateMesh_DiamondSquare( int LodLevels, int LodExtentTexels, Material material )
	{
		// FIXME: Use our own vertex layout, we only need a position
		var vertices = new List<PosAndLodVertex>( 32 );
		var indices = new List<int>();

		// Loop through each LOD level
		for ( int level = 0; level < LodLevels; level++ )
		{
			int step = 1 << level;
			int prevStep = Math.Max( 0, 1 << (level - 1) );

			int g = LodExtentTexels / 2;

			int pad = 1;
			int radius = step * (g + pad);

			for ( int y = -radius; y < radius; y += step )
			{
				for ( int x = -radius; x < radius; x += step )
				{
					if ( Math.Max( Math.Abs( x + prevStep ), Math.Abs( y + prevStep ) ) < (g * prevStep) )
						continue;

					//   A-----B-----C
					//   | \   |   / |
					//   |   \ | /   |
					//   D-----E-----F
					//   |   / | \   |
					//   | /   |   \ |
					//   G-----H-----I

					var vecA = new Vector3( x,        y,        level );
					var vecC = new Vector3( x + step, y,        level );
					var vecG = new Vector3( x,        y + step, level );
					var vecI = new Vector3( x + step, y + step, level );

					var vecB = (vecA + vecC) * 0.5f;
					var vecD = (vecA + vecG) * 0.5f;
					var vecF = (vecC + vecI) * 0.5f;
					var vecH = (vecG + vecI) * 0.5f;

					var vecE = (vecA + vecI) * 0.5f;

					vertices.Add( new PosAndLodVertex(vecA) ); // -9
					vertices.Add( new PosAndLodVertex(vecB) ); // -8
					vertices.Add( new PosAndLodVertex(vecC) ); // -7
					vertices.Add( new PosAndLodVertex(vecD) ); // -6
					vertices.Add( new PosAndLodVertex(vecE) ); // -5
					vertices.Add( new PosAndLodVertex(vecF) ); // -4
					vertices.Add( new PosAndLodVertex(vecG) ); // -3
					vertices.Add( new PosAndLodVertex(vecH) ); // -2
					vertices.Add( new PosAndLodVertex(vecI) ); // -1

					// Stitch the border into the next level
					if ( x == -radius )
					{
						indices.AddRange( new int[] { vertices.Count - 5, vertices.Count - 3, vertices.Count - 9 } ); // E G A
					}
					else
					{
						indices.AddRange( new int[] { vertices.Count - 5, vertices.Count - 6, vertices.Count - 9 } ); // E D A
						indices.AddRange( new int[] { vertices.Count - 5, vertices.Count - 3, vertices.Count - 6 } ); // E G D
					}

					if ( y == radius - 1 )
					{
						indices.AddRange( new int[] { vertices.Count - 5, vertices.Count - 1, vertices.Count - 3 } ); // E I G
					}
					else
					{
						indices.AddRange( new int[] { vertices.Count - 5, vertices.Count - 2, vertices.Count - 3 } ); // E H G
						indices.AddRange( new int[] { vertices.Count - 5, vertices.Count - 1, vertices.Count - 2 } ); // E I H
					}

					if ( x == radius - 1 )
					{
						indices.AddRange( new int[] { vertices.Count - 5, vertices.Count - 7, vertices.Count - 1 } ); // E C I
					}
					else
					{
						indices.AddRange( new int[] { vertices.Count - 5, vertices.Count - 4, vertices.Count - 1 } ); // E F I
						indices.AddRange( new int[] { vertices.Count - 5, vertices.Count - 7, vertices.Count - 4 } ); // E C F
					}

					if ( y == -radius )
					{
						indices.AddRange( new int[] { vertices.Count - 5, vertices.Count - 9, vertices.Count - 7 } ); // E A C
					}
					else
					{
						indices.AddRange( new int[] { vertices.Count - 5, vertices.Count - 8, vertices.Count - 7 } ); // E B C
						indices.AddRange( new int[] { vertices.Count - 5, vertices.Count - 9, vertices.Count - 8 } ); // E A B
					}
				}
			}
		}

		var mesh = new Mesh( material );
		mesh.CreateVertexBuffer( vertices.Count, PosAndLodVertex.Layout, vertices );
		mesh.CreateIndexBuffer( indices.Count, indices );
		return mesh;
	}
}
