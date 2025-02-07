using Sandbox.Sdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sandbox.Polygons;

internal static class Helpers
{
	public static Vector2 NormalizeSafe( in Vector2 vec )
	{
		var length = vec.Length;

		if ( length > 9.9999997473787516E-06 )
		{
			return vec / length;
		}
		else
		{
			return 0f;
		}
	}

	public static Vector2 Rotate90( Vector2 v )
	{
		return new Vector2( v.y, -v.x );
	}

	public static float Cross( Vector2 a, Vector2 b )
	{
		return a.x * b.y - a.y * b.x;
	}

	public static bool LineSegmentsIntersect( Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1 )
	{
		return Math.Sign( Cross( a0 - b0, b1 - b0 ) ) != Math.Sign( Cross( a1 - b0, b1 - b0 ) )
			&& Math.Sign( Cross( b0 - a0, a1 - a0 ) ) != Math.Sign( Cross( b1 - a0, a1 - a0 ) );
	}

	public static Vector3 RotateNormal( Vector3 oldNormal, float sin, float cos )
	{
		var normal2d = new Vector2( oldNormal.x, oldNormal.y );

		if ( normal2d.LengthSquared <= 0.000001f )
		{
			return oldNormal;
		}

		normal2d = NormalizeSafe( normal2d );

		return new Vector3( normal2d.x * cos, normal2d.y * cos, sin ).Normal;
	}

	public static float GetEpsilon( Vector2 vec, float frac = 0.0001f )
	{
		return Math.Max( Math.Abs( vec.x ), Math.Abs( vec.y ) ) * frac;
	}

	public static float GetEpsilon( Vector2 a, Vector2 b, float frac = 0.0001f )
	{
		return Math.Max( GetEpsilon( a, frac ), GetEpsilon( b, frac ) );
	}

	public static void UpdateMesh<T>( this Mesh mesh, VertexAttribute[] layout, List<T> vertices, List<int> indices )
		where T : unmanaged
	{
		if ( !mesh.HasIndexBuffer )
		{
			mesh.CreateVertexBuffer( vertices.Count, layout, vertices );
			mesh.CreateIndexBuffer( indices.Count, indices );
		}
		else if ( indices.Count > 0 && vertices.Count > 0 )
		{
			mesh.SetIndexBufferSize( indices.Count );
			mesh.SetVertexBufferSize( vertices.Count );

			mesh.SetVertexBufferData( vertices );
			mesh.SetIndexBufferData( indices );
		}

		mesh.SetIndexRange( 0, indices.Count );
	}
}

public record SdfDataDump(
	string Samples,
	int BaseIndex,
	int Size,
	int RowStride )
{
	internal SdfDataDump( Sdf2DArrayData data )
		: this( System.Convert.ToBase64String( data.Samples ), data.BaseIndex, data.Size, data.RowStride )
	{

	}
}

public record DebugDump(
	string Exception,
	string EdgeLoops,
	SdfDataDump SdfData,
	EdgeStyle EdgeStyle,
	float EdgeWidth,
	int EdgeFaces )
{
	public static string SeriaizeEdgeLoops( IReadOnlyList<IReadOnlyList<Vector2>> loops )
	{
		var writer = new StringWriter();

		foreach ( var loop in loops )
		{
			foreach ( var vertex in loop )
			{
				writer.Write( $"{vertex.x:R},{vertex.y:R};" );
			}

			writer.Write( "\n" );
		}

		return writer.ToString();
	}

	private static Regex Pattern { get; } = new Regex( @"(?<x>-?[0-9]+(?:\.[0-9]+)?),(?<y>-?[0-9]+(?:\.[0-9]+)?);" );

	public static IReadOnlyList<IReadOnlyList<Vector2>> DeserializeEdgeLoops( string source )
	{
		var loops = new List<IReadOnlyList<Vector2>>();

		foreach ( var line in source.Split( "\n" ) )
		{
			var loop = new List<Vector2>();

			foreach ( Match match in Pattern.Matches( line ) )
			{
				var x = float.Parse( match.Groups["x"].Value );
				var y = float.Parse( match.Groups["y"].Value );

				loop.Add( new Vector2( x, y ) );
			}

			if ( loop.Count == 0 )
			{
				break;
			}

			loops.Add( loop );
		}

		return loops;
	}

	public DebugDump Reduce()
	{
		var sourceLoops = DeserializeEdgeLoops( EdgeLoops );
		var erroring = new List<IReadOnlyList<Vector2>>();

		foreach ( var sourceLoop in sourceLoops )
		{
			var singleLoop = new[] { sourceLoop };

			try
			{
				using var polyMeshBuilder = PolygonMeshBuilder.Rent();

				Init( polyMeshBuilder, singleLoop );
				Bevel( polyMeshBuilder );
				Fill( polyMeshBuilder );

				continue;
			}
			catch
			{
				//
			}

			if ( sourceLoop.Count < 4 )
			{
				erroring.Add( sourceLoop );
			}

			var loop = sourceLoop.ToList();

			singleLoop[0] = loop;

			for ( var j = loop.Count - 1; j >= 0; --j )
			{
				var removed = loop[j];

				loop.RemoveAt( j );

				try
				{
					using var polyMeshBuilder = PolygonMeshBuilder.Rent();

					Init( polyMeshBuilder, singleLoop );
					Bevel( polyMeshBuilder );
					Fill( polyMeshBuilder );
				}
				catch
				{
					continue;
				}

				loop.Insert( j, removed );
			}

			erroring.Add( loop );
		}

		return this with { EdgeLoops = SeriaizeEdgeLoops( erroring ) };
	}

	public void Init( PolygonMeshBuilder meshBuilder )
	{
		Init( meshBuilder, DeserializeEdgeLoops( EdgeLoops ) );
	}

	private static void Init( PolygonMeshBuilder meshBuilder, IReadOnlyList<IReadOnlyList<Vector2>> loops )
	{
		foreach ( var loop in loops )
		{
			meshBuilder.AddEdgeLoop( loop, 0, loop.Count );
		}
	}

	public void Bevel( PolygonMeshBuilder meshBuilder, float? width = null )
	{
		var w = width ?? EdgeWidth;
		var style = width is null ? EdgeStyle : EdgeStyle.Bevel;

		switch ( style )
		{
			case EdgeStyle.Sharp:
				break;

			case EdgeStyle.Bevel:
				meshBuilder.Bevel( w, w );
				break;

			case EdgeStyle.Round:
				meshBuilder.Arc( w, w, EdgeFaces );
				break;
		}
	}

	public void Fill( PolygonMeshBuilder meshBuilder )
	{
		meshBuilder.Fill();
	}
}
