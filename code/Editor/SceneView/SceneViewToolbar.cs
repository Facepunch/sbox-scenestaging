using System;

public class SceneViewToolbar : SceneToolbar
{
	public SceneViewToolbar( Widget parent) : base( parent )
	{

	}

	public class ShadingButton : Button
	{
		public ShadingButton() : base( null )
		{
			Text = "Shaded";
			MinimumSize = new Vector2( 128, 23 );
			// SetStyles( "padding: 5px 64px 5px 32px;" );
		}

		protected override void OnClicked()
		{
			var menu = new Menu( this );

			var shaded = menu.AddOption( "Shaded" );
			shaded.Checkable = true;
			shaded.Checked = true;
			menu.AddOption( "Wireframe" );
			menu.AddOption( "Shaded Wireframe" );

			menu.AddSeparator();

			var a = menu.AddMenu( "Tool Visualizations" );

			// AddOptions<EngineViewRenderMode>( a, "mat_fullbright" );

			menu.OpenAtCursor();
		}

		protected override void OnPaint()
		{
			Paint.Antialiasing = true;
			Paint.ClearPen();

			if ( Paint.HasMouseOver )
			{
				Paint.SetBrush( Color.Parse( "#2a5f7d" ) ?? default );
				Paint.DrawRect( LocalRect, Theme.ControlRadius );
			}

			// Paint.SetBrush( Theme.ControlBackground );
			// Paint.SetBrush( Theme.ButtonDefault );
			// Paint.DrawRect( LocalRect, Theme.ControlRadius );

			// 42 95 125

			var fg = Theme.White;

			// if ( !HasActiveOptions ) fg = fg.WithAlphaMultiplied( 0.3f );

			Paint.SetPen( fg.WithAlphaMultiplied( Paint.HasMouseOver ? 1.0f : 0.9f ) );
			Paint.DrawIcon( LocalRect.Shrink( 8, 0, 0, 0 ), "brightness_4", 14, TextFlag.LeftCenter );
			Paint.DrawText( LocalRect.Shrink( 32, 0, 0, 0 ), "Shaded", TextFlag.LeftCenter );

			Paint.DrawIcon( LocalRect.Shrink( 4, 0 ), "arrow_drop_down", 18, TextFlag.RightCenter );
		}
	}

	public override void BuildToolbar()
	{
		//
		var option = new Option( "Shading Mode", "invert_colors" ) { Triggered = () =>
		{
			var menu = new Menu( this );

			var shaded = menu.AddOption( "Shaded" );
			shaded.Checkable = true;
			shaded.Checked = true;
			menu.AddOption( "Wireframe" );
			menu.AddOption( "Shaded Wireframe" );

			menu.AddSeparator();

			var a = menu.AddMenu( "Tool Visualizations" );

			AddOptions<EngineViewRenderMode>( a, "mat_fullbright" );

			menu.OpenAtCursor();
		}
		};


		// AddWidget( new ShadingButton() );

		AddOption( option );
		AddOption( "2D" );

		{
			var o = new Option( "Lighting", "lightbulb" );
			o.Checkable = true;
			o.Checked = true;// !(SceneInstance?.GetValue<bool>( "unlit" ) ?? false );
			o.Toggled = v => SceneInstance.SetValue( "unlit", !v );
			AddOption( o );
		}

		// AddOption( option );

		AddSeparator();

		AddGizmoModes();
		AddSeparator();
		AddCameraDropdown();
		AddSeparator();
		AddAdvancedDropdown();
	}

	private void AddOptions<T>( Menu menu, string convar ) where T : System.Enum
	{
		foreach ( var e in DisplayInfo.ForEnumValues<T>() )
		{
			var intValue = Convert.ToInt32( e.value );
			var val = $"{intValue}";

			var o = new Option( e.info.Name, e.info.Icon );
			o.Checkable = true;
			o.Checked = SceneInstance.GetValue<int>( "ToolsVisMode" ) == intValue;
			o.Toggled = v =>
			{
				SceneInstance.SetValue( "ToolsVisMode", v ? intValue : 0 );
				// UpdateButtonText();
			};

			menu.AddOption( o );
		}
	}

	public enum EngineViewRenderMode
	{
		[Title( "Full Bright" ), Icon( "lightbulb" )]
		FullBright = 1,

		[Title( "Diffuse Lighting" ), Icon( "cloud" )]
		Diffuse = 2,

		[Title( "Specular Lighting" ), Icon( "flare" )]
		Specular = 3,

		[Title( "Transmissive Lighting" ), Icon( "opacity" )]
		Transmissive = 4,

		[Title( "Lighting Complexity" ), Icon( "layers" )]
		LightingComplexity = 5,

		[Title( "UV Maps" ), Icon( "gradient" )]
		ShowUV = 6,

		[Title( "Indexed Light Count" ), Icon( "format_list_numbered" )]
		IndexedLightCount = 7,

		[Title( "Albedo" ), Icon( "palette" )]
		Albedo = 10,

		[Title( "Reflectivity" ), Icon( "reflect_horizontal" )]
		Reflectivity = 11,

		[Title( "Roughness" ), Icon( "texture" )]
		Roughness = 12,

		[Title( "Reflectance" ), Icon( "photo" )]
		Reflectance = 13,

		[Title( "Diffuse Ambient Occlusion" ), Icon( "grain" )]
		DiffuseAmbientOcclusion = 14,

		[Title( "Specular Ambient Occlusion" ), Icon( "grain" )]
		SpecularAmbientOcclusion = 15,

		[Title( "Shader IDs" ), Icon( "sell" )]
		ShaderIDColor = 16,

		[Title( "Cubemap Reflections" ), Icon( "filter_hdr" )]
		CubemapReflections = 17,

		[Title( "Normal TS" ), Icon( "shuffle" )]
		NormalTs = 20,

		[Title( "Normal WS" ), Icon( "shuffle" )]
		NormalWs = 21,

		[Title( "Tangent U WS" ), Icon( "shuffle" )]
		TangentUWs = 22,

		[Title( "Tangent V WS" ), Icon( "shuffle" )]
		TangentVWs = 23,

		[Title( "Geometric Normal WS" ), Icon( "shuffle" )]
		GeometricNormalWs = 24,

		[Title( "Bent Normal WS" ), Icon( "shuffle" )]
		BentNormalWs = 25,

		[Title( "Bent Geometric Normal WS" ), Icon( "shuffle" )]
		BentGeometricNormalWs = 26,

		[Title( "Geometric Roughness" ), Icon( "tune" )]
		GeometricRoughness = 30,

		[Title( "Curvature" ), Icon( "tune" )]
		Curvature = 31,

		[Title( "Eyes/Mouth Mask" ), Icon( "face" )]
		EyesMouthMask = 32,

		[Title( "Wrinkle" ), Icon( "texture" )]
		Wrinkle = 33,


		[Title( "Baked Light Result" ), Icon( "hdr_weak" )]
		BakedLightResult = 40,

		[Title( "Baked Light Diffuse" ), Icon( "hdr_weak" )]
		BakedLightDiffuse = 41,

		[Title( "Baked Light Specular" ), Icon( "hdr_weak" )]
		BakedLightSpecular = 42,

		[Title( "Baked Light Specular with Probes" ), Icon( "hdr_weak" )]
		BakedLightSpecularWithProbes = 43,

		[Title( "Baked Light Ambient" ), Icon( "hdr_weak" )]
		BakedLightAmbient = 44,

		[Title( "Baked Light Ambient Occlusion" ), Icon( "hdr_weak" )]
		BakedLightAmbientOcclusion = 45,

		[Title( "Baked Light Light Direction" ), Icon( "hdr_weak" )]
		BakedLightLightDirection = 46,

		[Title( "Baked Light Light Color" ), Icon( "hdr_weak" )]
		BakedLightLightColor = 47,

		[Title( "Baked Light Light Chart" ), Icon( "hdr_weak" )]
		BakedLightLightChart = 48,

		[Title( "Baked Light Count" ), Icon( "hdr_weak" )]
		BakedLightCount = 49,

		[Title( "Tiled Rendering Lights" ), Icon( "view_module" )]
		TiledRenderingQuads = 50,

		[Title( "Quad Overdraw" ), Icon( "signal_cellular_null" )]
		QuadOverdraw = 100,
	}
}
