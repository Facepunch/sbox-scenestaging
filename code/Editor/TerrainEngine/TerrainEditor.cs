using Sandbox.TerrainEngine;
using System;

namespace Editor.TerrainEngine;

public static class TerrainEditor
{
	public static string Mode { get; set; } = "Sculpt";

	public static Brush Brush { get; set; } = new();

	public static void AddHeight( Terrain terrain, Vector3 pos, bool invert = false )
	{
		// this should really interpolate, but just round to ints for now
		int basex = (int)Math.Round( pos.x / terrain.TerrainResolutionInInches );
		int basey = (int)Math.Round( pos.y / terrain.TerrainResolutionInInches );

		var radius = (int)Math.Round( Brush.Size / terrain.TerrainResolutionInInches );

		var x1 = basex - radius;
		var y1 = basey - radius;
		var x2 = basex + radius;
		var y2 = basey + radius;

		var size = radius * 2;

		for ( var y = 0; y < size; ++y )
		{
			for ( var x = 0; x < size; ++x )
			{
				var brushWidth = Brush.Texture.Width;
				var brushHeight = Brush.Texture.Height;

				var brushX = (brushWidth / size) * x;
				var brushY = (brushHeight / size) * y;

				var brushPix = Brush.Pixels[brushY * brushHeight + brushX];

				float brushValue = ((float)brushPix.r / 255.0f) * 100;
				var value = (int)Math.Round( brushValue * Brush.Opacity );

				if ( invert ) value = -value;

				var height = terrain.TerrainData.GetHeight( x1 + x, y1 + y );
				int newHeight = height + value;

				newHeight = Math.Max( newHeight, 0 );
				terrain.TerrainData.SetHeight( x1 + x, y1 + y, (ushort)newHeight );
			}
		}
	}

	public static void AddSplat( Terrain terrain, Vector3 pos, bool invert = false )
	{
		// this should really interpolate, but just round to ints for now
		int basex = (int)Math.Round( pos.x / terrain.TerrainResolutionInInches );
		int basey = (int)Math.Round( pos.y / terrain.TerrainResolutionInInches );

		var radius = (int)Math.Round( Brush.Size / terrain.TerrainResolutionInInches );

		var x1 = basex - radius;
		var y1 = basey - radius;
		var x2 = basex + radius;
		var y2 = basey + radius;

		var size = radius * 2;

		for ( var y = 0; y < size; ++y )
		{
			for ( var x = 0; x < size; ++x )
			{
				var brushWidth = Brush.Texture.Width;
				var brushHeight = Brush.Texture.Height;

				var brushX = (brushWidth / size) * x;
				var brushY = (brushHeight / size) * y;

				var brushPix = Brush.Pixels[brushY * brushHeight + brushX];

				float brushValue = ((float)brushPix.r / 255.0f);
				var value = (int)Math.Round( brushValue * Brush.Opacity );

				if ( invert ) value = -value;

				var color = terrain.TerrainData.GetSplat( x1 + x, y1 + y );
				
				if ( !invert )
				{
					color.r = (byte)Math.Clamp( brushPix.r + color.r, 0, 255 );
				}
				else
				{
					color.g = (byte)Math.Clamp( brushPix.r + color.g, 0, 255 );

				}

				terrain.TerrainData.SetSplat( x1 + x, y1 + y, color );
				// var height = terrain.TerrainData.GetHeight( x1 + x, y1 + y );
				//terrain.TerrainData.SetHeight( x1 + x, y1 + y, (ushort)(height + value) );
			}
		}
	}

	public static bool Update( Gizmo.Instance instance, SceneCamera camera, Widget canvas )
	{
		// poo emoji
		_previewObject?.Delete();
		_previewObject = default;

		// Only do stuff if we have a terrain selected, this is like a special control mode
		var selection = instance.Selection.FirstOrDefault();
		if ( selection is not GameObject gameObject )
			return false;
		if ( !gameObject.Components.TryGet<Terrain>( out var terrain ) )
			return false;

		if ( terrain.RayIntersects( instance.Input.CursorRay, out var hitPosition ) && canvas.IsUnderMouse )
		{
			DrawBrushPreview( instance, new Transform( hitPosition ) );

			if ( Application.MouseButtons.HasFlag( MouseButtons.Left ) )
			{
				if ( TerrainEditor.Mode == "Sculpt" )
				{
					AddHeight( terrain, hitPosition, Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Ctrl ) );
				}
				else if ( TerrainEditor.Mode == "Paint" )
				{
					AddSplat( terrain, hitPosition, Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Ctrl ) );
				}
				terrain.SyncHeightMap();
			}

			return true;
		}

		return false;
	}

	static BrushPreviewSceneObject _previewObject;

	static void DrawBrushPreview( Gizmo.Instance instance, Transform transform )
	{
		if ( _previewObject == null )
		{
			_previewObject = new BrushPreviewSceneObject( instance.World ); // Not cached, FindOrCreate is internal :x
		}

		var color = Color.White;

		if ( Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Ctrl ) )
			color = Color.Red;

		color.a = Brush.Opacity;

		_previewObject.RenderLayer = SceneRenderLayer.OverlayWithDepth;
		_previewObject.Bounds = new BBox( 0, float.MaxValue );
		_previewObject.Transform = transform;
		_previewObject.Radius = Brush.Size;
		_previewObject.Texture = Brush.Texture;
		_previewObject.Color = color;
	}
}
