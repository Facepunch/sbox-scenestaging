using Sandbox;
using System;
using System.Linq;

namespace Editor;

/// <summary>
/// A popup dialog to select an entity type
/// </summary>
public partial class GameObjectTypeSelector : Editor.Widgets.SelectionDialog<TypeDescription>
{	
	public GameObjectTypeSelector( TypeDescription[] types, Action<TypeDescription> create )
	{
		WindowTitle = "Select Game Object Type..";
		
		FixedWidth = 380;
		FixedHeight = 600;

		AddSearchHeader();

		var lv = new ListView();

		lv.SetItems( types );
		lv.ItemSize = new Vector2( -1, 48 );
		lv.ItemSelected += x =>
		{
			if ( x is TypeDescription type )
			{
				SelectItem( type );
			}
		};

		lv.ItemActivated += ( x ) => OnSelectionComplete();

		lv.ItemPaint = PaintItem;
		lv.OnPaintOverride = () =>
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.ControlBackground );
			Paint.DrawRect( lv.LocalRect );
			return false;
		};

		SearchField.ForwardNavigationEvents = lv;
		OnSelectionFinished = create;

		OnSearchFilter = ( t ) =>
		{
			lv.SetItems( types.Where( x => x.Title.Contains( t, StringComparison.OrdinalIgnoreCase ) ) );
		};

		BodyLayout.Add( lv, 1 );
	}

	private void PaintItem( VirtualWidget obj )
	{
		obj.PaintBackground( Theme.WidgetBackground.WithAlpha( 0.5f ), 0 );

		Paint.SetPen( obj.GetForegroundColor() );

		if ( obj.Object is TypeDescription type )
		{
			var r = obj.Rect.Shrink( 16, 7 );

			Paint.SetDefaultFont( 10, 500 );
			Paint.DrawText( r, type.Title, TextFlag.LeftTop );

			Paint.SetDefaultFont( );
			Paint.SetPen( obj.GetForegroundColor().WithAlpha( 0.5f ) );
			Paint.DrawText( r, $"{type.ClassName}, {type.FullName}", TextFlag.LeftBottom );
		}
	}

	public static void Open( Action<TypeDescription> create )
	{
		var entityTypes = EditorTypeLibrary.GetTypes<IPrefabObject>()
								.Where( x => !x.IsGenericType )
								.OrderBy( x => x.ClassName )
								.ToArray();

		if ( entityTypes.Length == 1 )
		{
			create( entityTypes[0] );
			return;
		}

		var s = new GameObjectTypeSelector( entityTypes, create );
		s.DoneButton.Text = "Add";
		s.OpenBelowCursor( 0 );
	}
}
