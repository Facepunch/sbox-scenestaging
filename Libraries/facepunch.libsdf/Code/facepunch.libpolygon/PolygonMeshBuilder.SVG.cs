using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sandbox.Utility.Svg;

namespace Sandbox.Polygons;

/// <summary>
/// Options for <see cref="PolygonMeshBuilder.AddSvg"/>.
/// </summary>
public class AddSvgOptions
{
	public static AddSvgOptions Default { get; } = new();

	/// <summary>
	/// If true, any unsupported path types will throw an exception. Defaults to false.
	/// </summary>
	public bool ThrowIfNotSupported { get; set; }

	/// <summary>
	/// Maximum distance between vertices on curved paths. Defaults to 1.
	/// </summary>
	public float CurveResolution { get; set; } = 1f;

    public bool KeepAspectRatio { get; set; } = true;
}

partial class PolygonMeshBuilder
{
	/// <summary>
	/// Add all supported paths from the given SVG document.
	/// </summary>
	/// <param name="contents">SVG document contents.</param>
	/// <param name="options">Options for generating vertices from paths.</param>
	/// <param name="targetBounds">Rescale and translate the imported SVG to fill the given bounds</param>
	public PolygonMeshBuilder AddSvg( string contents, AddSvgOptions options = null, Rect? targetBounds = null )
    {
        options ??= AddSvgOptions.Default;

        var svg = SvgDocument.FromString( contents );

		if ( svg.Paths.Count == 0 )
		{
			return this;
		}

		if ( targetBounds == null )
		{
			foreach ( var path in svg.Paths )
			{
				AddPath( path, options );
			}

			return this;
		}

		var bounds = svg.Paths[0].Bounds;

		foreach ( var path in svg.Paths )
		{
			bounds.Add( path.Bounds );
		}

		var scale = targetBounds.Value.Size / bounds.Size;
        var aspectOffset = Vector2.Zero;

        if ( options.KeepAspectRatio )
        {
            var oldScale = scale;

            scale = Math.Min( scale.x, scale.y );
            aspectOffset = (oldScale - scale) * targetBounds.Value.Size * 0.25f;
        }

		var offset = targetBounds.Value.Position - bounds.Position * scale + aspectOffset;

		foreach ( var path in svg.Paths )
		{
			AddPath( path, options, offset, scale );
		}

		return this;
	}

	private static void ThrowNotSupported( AddSvgOptions options, string message )
	{
		if ( !options.ThrowIfNotSupported )
		{
			return;
		}

		throw new NotImplementedException( $"SVG path element not supported: {message}" );
	}

	/// <summary>
	/// Add an individual path from an SVG document, if supported.
	/// </summary>
	/// <param name="path">SVG path element.</param>
	/// <param name="options">Options for generating vertices from paths.</param>
	/// <param name="targetBounds">Rescale and translate the imported SVG to fill the given bounds</param>
	public PolygonMeshBuilder AddPath( SvgPath path, AddSvgOptions options = null )
	{
		options ??= AddSvgOptions.Default;
		return AddPath( path, options, Vector2.Zero, Vector2.One );
	}

	private PolygonMeshBuilder AddPath( SvgPath path, AddSvgOptions options, Vector2 offset, Vector2 scale )
	{
		if ( path.IsEmpty )
		{
			return this;
		}

		if ( path.FillColor == null )
		{
			return this;
		}

		if ( path.FillType != PathFillType.Winding )
		{
			if ( options.ThrowIfNotSupported )
			{
				//throw new NotImplementedException( "Only fill-type: winding is supported." );
			}

			//return this;
		}

		var openPath = new List<Vector2>();
		var last = Vector2.Zero;

		foreach ( var cmd in path.Commands )
		{
			switch ( cmd )
			{
				case AddPolyPathCommand addPolyPathCommand:
					AddPolyPath( addPolyPathCommand, options, offset, scale );
					break;

				case AddCirclePathCommand addCirclePathCommand:
					AddCirclePath( addCirclePathCommand, options, openPath, offset, scale );
					break;

				case MoveToPathCommand moveToPathCommand:
					openPath.Clear();
					openPath.Add( new Vector2( moveToPathCommand.X, moveToPathCommand.Y ) );
					break;

				case LineToPathCommand lineToPathCommand:
					openPath.Add( new Vector2( lineToPathCommand.X, lineToPathCommand.Y ) );
					break;

				case CubicToPathCommand cubicToPathCommand:
					CubicToPath( cubicToPathCommand, options, openPath, last );
					break;

				case ClosePathCommand:
					if ( openPath.Count >= 3 )
					{
						AddEdgeLoop( openPath, 0, openPath.Count, offset, scale );
					}

					openPath.Clear();
					break;

				default:
					ThrowNotSupported( options, $"{cmd.GetType()}" );
					break;
			}

			if ( openPath.Count > 0 )
			{
				last = openPath[^1];
			}
		}

		return this;
	}

	private void AddPolyPath( AddPolyPathCommand cmd, AddSvgOptions options, Vector2 offset, Vector2 scale )
	{
		if ( !cmd.Close )
		{
			return;
		}

		AddEdgeLoop( cmd.Points, 0, cmd.Points.Count, offset, scale );
	}

	private void AddCirclePath( AddCirclePathCommand cmd, AddSvgOptions options, List<Vector2> openPath, Vector2 offset, Vector2 scale )
	{
		openPath.Clear();

		var center = new Vector2( cmd.X, cmd.Y );

		for ( var i = 23; i >= 0; i-- )
		{
			var r = i * (MathF.PI * 2f / 24f);

			var cos = MathF.Cos( r );
			var sin = MathF.Sin( r );

			openPath.Add( new Vector2( cos, sin ) * cmd.Radius + center );
		}

		AddEdgeLoop( openPath, 0, openPath.Count, offset, scale );
	}

	private void CubicToPath( CubicToPathCommand cmd, AddSvgOptions options, List<Vector2> openPath, Vector2 last )
	{
		var pointCount = 6;
		var tScale = 1f / pointCount;

		for ( var i = 0; i < pointCount; i++ )
		{
			var t = (i + 1) * tScale;
			var s = 1f - t;

			var a = s * s * s;
			var b = 3f * s * s * t;
			var c = 3f * s * t * t;
			var d = t * t * t;

			var p0 = last;
			var p1 = new Vector2( cmd.X0, cmd.Y0 );
			var p2 = new Vector2( cmd.X1, cmd.Y1 );
			var p3 = new Vector2( cmd.X2, cmd.Y2 );

			openPath.Add( p0 * a + p1 * b + p2 * c + p3 * d );
		}
	}

	public string ToSvg()
	{
		var openEdges = new HashSet<int>( _activeEdges );
		var writer = new StringWriter();

		writer.WriteLine( "<svg xmlns=\"http://www.w3.org/2000/svg\">" );

		while ( openEdges.Count > 0 )
		{
			var firstIndex = openEdges.First();

			var edge = _allEdges[firstIndex];

			writer.Write( "  <polygon points=\"" );

			while ( true )
			{
				writer.Write( $"{edge.Origin.x:R},{edge.Origin.y:R} " );
				openEdges.Remove( edge.Index );

				if ( edge.NextEdge == firstIndex )
				{
					break;
				}

				edge = _allEdges[edge.NextEdge];
			}

			writer.WriteLine("\" fill=\"black\" stroke=\"red\" />");
		}

		writer.WriteLine( @"</svg>" );

		return writer.ToString();
	}
}
