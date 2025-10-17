using System;
using static Sandbox.ClutterInstance;

namespace Editor;

[CustomEditor( typeof( List<ClutterLayer> ), NamedEditor = "clutter_layer_multi" )]
public class ClutterLayerMultiControlWidget : ControlWidget
{
	public override bool IsControlActive => base.IsControlActive || _menu.IsValid();
	public override bool IsControlButton => true;
	public override bool IsControlHovered => base.IsControlHovered || _menu.IsValid();

	PopupWidget _menu;

	public ClutterLayerMultiControlWidget( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;
		Layout = Layout.Row();
		Layout.Spacing = 2;
	}

	protected override void PaintControl()
	{
		var currentValue = SerializedProperty.GetValue<List<ClutterLayer>>() ?? [];

		// Get all layers from the ClutterSystem
		var activeScene = SceneEditorSession.Active?.Scene;
		List<ClutterLayer> enabledLayers = [];
		if ( activeScene != null )
		{
			var clutterSystem = activeScene.GetSystem<ClutterSystem>();
			if ( clutterSystem != null )
			{
				enabledLayers.AddRange( clutterSystem.GetAllLayers() );
			}
		}

		var color = IsControlHovered ? Theme.Blue : Theme.TextControl;
		if ( IsControlDisabled ) color = color.WithAlpha( 0.5f );

		var rect = LocalRect.Shrink( 8, 0 );

		if ( enabledLayers.Count > 0 )
		{
			var selectedLayers = currentValue.Where( l => enabledLayers.Contains( l ) ).ToList();

			string displayText;
			if ( selectedLayers.Count == 0 )
			{
				displayText = "No Layers";
			}
			else if ( selectedLayers.Count == enabledLayers.Count )
			{
				displayText = "All Layers";
			}
			else if ( selectedLayers.Count == 1 )
			{
				displayText = selectedLayers[0].Name;
			}
			else
			{
				displayText = string.Empty;
				for ( int i = 0; i < selectedLayers.Count; i++ )
				{
					displayText += selectedLayers[i].Name;
					if ( i != selectedLayers.Count - 1 )
					{
						displayText += ", ";
					}
				}
			}

			Paint.SetPen( color );
			Paint.DrawText( rect, displayText, TextFlag.LeftCenter );
		}
		else
		{
			Paint.SetPen( color.WithAlpha( 0.5f ) );
			Paint.DrawText( rect, "No Layers", TextFlag.LeftCenter );
		}

		Paint.SetPen( color );
		Paint.DrawIcon( rect, "Arrow_Drop_Down", 17, TextFlag.RightCenter );
	}

	public override void StartEditing()
	{
		if ( IsControlDisabled ) return;

		if ( !_menu.IsValid )
		{
			OpenMenu();
		}
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( IsControlDisabled ) return;

		if ( e.LeftMouseButton && !_menu.IsValid() )
		{
			OpenMenu();
		}
	}

	void ToggleLayer( ClutterLayer layer )
	{
		var currentValue = SerializedProperty.GetValue<List<ClutterLayer>>() ?? new List<ClutterLayer>();

		if ( currentValue.Contains( layer ) )
		{
			currentValue.Remove( layer );
		}
		else
		{
			currentValue.Add( layer );
		}

		SerializedProperty.SetValue( currentValue );
	}

	void OpenMenu()
	{
		PropertyStartEdit();

		var currentValue = SerializedProperty.GetValue<List<ClutterLayer>>() ?? new List<ClutterLayer>();

		// Get all layers from the ClutterSystem
		var activeScene = SceneEditorSession.Active?.Scene;
		var enabledLayers = new List<ClutterLayer>();
		if ( activeScene != null )
		{
			var clutterSystem = activeScene.GetSystem<ClutterSystem>();
			if ( clutterSystem != null )
			{
				enabledLayers.AddRange( clutterSystem.GetAllLayers() );
			}
		}

		if ( enabledLayers.Count == 0 )
		{
			return;
		}

		_menu = new PopupWidget( null );
		_menu.Layout = Layout.Column();
		_menu.MinimumWidth = ScreenRect.Width;
		_menu.MaximumWidth = ScreenRect.Width;
		_menu.OnLostFocus += PropertyFinishEdit;

		var scroller = _menu.Layout.Add( new ScrollArea( this ), 1 );
		scroller.Canvas = new Widget( scroller )
		{
			Layout = Layout.Column(),
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand,
			MaximumWidth = ScreenRect.Width
		};

		foreach ( var layer in enabledLayers )
		{
			var option = scroller.Canvas.Layout.Add( new LayerMenuOption( layer, () =>
			{
				var current = SerializedProperty.GetValue<List<ClutterLayer>>() ?? [];
				return current.Contains( layer );
			} ) );
			option.MouseLeftPress = () =>
			{
				ToggleLayer( layer );
				_menu.Update();
			};
		}

		_menu.Position = ScreenRect.BottomLeft;
		_menu.Visible = true;
		_menu.AdjustSize();
		_menu.ConstrainToScreen();
		_menu.OnPaintOverride = PaintMenuBackground;

		if ( scroller.VerticalScrollbar.Minimum != scroller.VerticalScrollbar.Maximum )
		{
			scroller.Canvas.MaximumWidth -= 8; // leave some space for the scrollbar
		}
	}

	bool PaintMenuBackground()
	{
		Paint.SetBrushAndPen( Theme.ControlBackground );
		Paint.DrawRect( Paint.LocalRect, 0 );
		return true;
	}
}

file class LayerMenuOption : Widget
{
	ClutterLayer layer;
	Func<bool> isSelectedFunc;

	public LayerMenuOption( ClutterLayer l, Func<bool> isSelectedFunction ) : base( null )
	{
		layer = l;
		isSelectedFunc = isSelectedFunction;

		Layout = Layout.Row();
		Layout.Margin = 8;
		VerticalSizeMode = SizeMode.CanGrow;

		Layout.Add( new IconButton( "layers" ) { Background = Color.Transparent, TransparentForMouseEvents = true, IconSize = 18 } );
		Layout.AddSpacingCell( 8 );

		var c = Layout.AddColumn();
		var title = c.Add( new Label( layer.Name ) );
		title.SetStyles( $"font-size: 12px; font-weight: bold; font-family: {Theme.DefaultFont}; color: white;" );

		var objectCount = layer.Objects?.Count ?? 0;
		var desc = c.Add( new Label( $"{objectCount} objects" ) );
		desc.SetStyles( $"font-size: 11px; font-family: {Theme.DefaultFont}; color: #aaa;" );
	}

	protected override void OnPaint()
	{
		var isSelected = isSelectedFunc?.Invoke() ?? false;
		if ( Paint.HasMouseOver || isSelected )
		{
			Paint.SetBrushAndPen( Theme.Blue.WithAlpha( isSelected ? 0.3f : 0.1f ) );
			Paint.DrawRect( LocalRect.Shrink( 2 ), 2 );
		}
	}
}
