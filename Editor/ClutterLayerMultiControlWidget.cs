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
	List<ClutterLayer> _cachedLayers;
	bool _needsRebuild = true;

	// Access to the internal _selectedLayerIds field for stable persistence
	SerializedProperty InternalIdsProperty => SerializedProperty.Parent?.GetProperty( "_selectedLayerIds" );

	public ClutterLayerMultiControlWidget( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;
		Layout = Layout.Row();
		Layout.Spacing = 2;
	}

	public void InvalidateCache()
	{
		_needsRebuild = true;
		_cachedLayers = null;
	}

	List<ClutterLayer> GetAvailableLayers()
	{
		if ( _cachedLayers != null && !_needsRebuild )
		{
			return _cachedLayers;
		}

		// Get all layers from the ClutterSystem and any assigned brush
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

		// Also include layers from brush resource if this is on a ClutterVolumeComponent
		if ( SerializedProperty.Parent != null )
		{
			var targetObject = SerializedProperty.Parent.Targets.FirstOrDefault();
			if ( targetObject is ClutterVolumeComponent volume && volume.Brush != null )
			{
				if ( volume.Brush.Layers != null )
				{
					// Add brush layers that aren't already in the list (by ID)
					foreach ( var brushLayer in volume.Brush.Layers )
					{
						if ( !enabledLayers.Any( l => l.Id == brushLayer.Id ) )
						{
							Log.Info( "Added layers: " + brushLayer.Name );
							enabledLayers.Add( brushLayer );
						}
					}
				}
			}
		}

		_cachedLayers = enabledLayers;
		_needsRebuild = false;
		return _cachedLayers;
	}

	protected override void PaintControl()
	{
		// Get the internal IDs list
		var selectedIds = InternalIdsProperty?.GetValue<List<Guid>>() ?? [];
		var enabledLayers = GetAvailableLayers();

		var color = IsControlHovered ? Theme.Blue : Theme.TextControl;
		if ( IsControlDisabled ) color = color.WithAlpha( 0.5f );

		var rect = LocalRect.Shrink( 8, 0 );

		if ( enabledLayers.Count > 0 )
		{
			// Match by ID
			var selectedLayers = enabledLayers.Where( l => selectedIds.Contains( l.Id ) ).ToList();

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
				displayText = string.Join( ", ", selectedLayers.Select( l => l.Name ) );
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

	void ToggleLayer( Guid layerId )
	{
		if ( InternalIdsProperty == null ) return;

		var currentIds = InternalIdsProperty.GetValue<List<Guid>>() ?? new List<Guid>();

		if ( currentIds.Contains( layerId ) )
		{
			currentIds.Remove( layerId );
		}
		else
		{
			currentIds.Add( layerId );
		}

		// SetValue on the internal IDs field directly - this is what gets serialized
		InternalIdsProperty.SetValue( currentIds );
	}

	void OpenMenu()
	{
		PropertyStartEdit();

		var enabledLayers = GetAvailableLayers();

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
				var currentIds = InternalIdsProperty?.GetValue<List<Guid>>() ?? [];
				return currentIds.Contains( layer.Id );
			} ) );
			option.MouseClick = () =>
			{
				Log.Info( "press" );
				ToggleLayer( layer.Id );
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
		Cursor = CursorShape.Finger;

		var icon = new IconButton( "layers" ) { Background = Color.Transparent, TransparentForMouseEvents = true, IconSize = 18 };
		icon.Cursor = CursorShape.Finger;
		Layout.Add( icon );
		Layout.AddSpacingCell( 8 );

		var c = Layout.AddColumn();
		var title = c.Add( new Label( layer.Name ) );
		title.SetStyles( $"font-size: 12px; font-weight: bold; font-family: {Theme.DefaultFont}; color: white;" );

		var objectCount = layer.Objects?.Count ?? 0;
		var desc = c.Add( new Label( $"{objectCount} objects" ) );
		desc.SetStyles( $"font-size: 11px; font-family: {Theme.DefaultFont}; color: #aaa;" );
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( e.LeftMouseButton )
		{
			MouseLeftPress?.Invoke();
			e.Accepted = true;
			Update();
		}

		base.OnMouseClick( e );
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
