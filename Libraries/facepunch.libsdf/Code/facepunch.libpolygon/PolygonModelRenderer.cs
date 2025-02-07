
using System;
using System.Collections.Generic;

namespace Sandbox.Polygons;

public class PolygonModelRenderer : ModelRenderer
{
	private Mesh _mesh;

	private string _svg;
	private bool _meshDirty;

	/// <summary>
	/// Scalable Vector Graphics source string for this model.
	/// </summary>
	[Property]
	public string Svg
	{
		get => _svg;
		set
		{
			_svg = value;
			_meshDirty = true;
		}
	}

	private int _lastHash = 0;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		UpdateModel();
	}

	protected override void OnValidate()
	{
		base.OnValidate();

		_meshDirty = true;
	}

	private void UpdateModel()
	{
		if ( !_meshDirty )
		{
			return;
		}

		var hash = Svg?.FastHash() ?? 0;
		if ( _lastHash == hash )
		{
			return;
		}

		if ( Model?.IsProcedural is not true )
		{
			Model = null;
		}

		_lastHash = hash;

		if ( !string.IsNullOrEmpty( Svg ) )
		{
			using var builder = PolygonMeshBuilder.Rent();

			builder.MaxSmoothAngle = 33f.DegreeToRadian();

			builder.AddSvg( _svg, new AddSvgOptions
			{
				ThrowIfNotSupported = true
			}, new Rect( -128f, -128f, 256f, 256f ) );
			builder.Extrude( 8f );
			builder.Arc( 2f, 2 );
			builder.Fill();
			builder.Mirror();

			_mesh ??= new Mesh( Material.Load( "materials/default/white.vmat" ) );
			_mesh.UpdateMesh( PolygonMeshBuilder.Vertex.Layout, builder.Vertices, builder.Indices );
			
			Model ??= new ModelBuilder()
				.AddMesh( _mesh )
				.Create();
		}
		else
		{
			_mesh?.SetIndexRange( 0, 0 );
		}
	}

	protected override void OnUpdate()
	{
		UpdateModel();

		base.OnUpdate();
	}
}
