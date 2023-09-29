using Sandbox;
using Sandbox.Helpers;
using System;
using System.Linq;
using System.Reflection;
using static Sandbox.Event;

namespace Editor.EntityPrefabEditor;

/// <summary>
/// A popup dialog to select an entity type
/// </summary>
public partial class ComponentTypeSelector : PopupWidget
{
	string currentCategory;
	string searchString;
	ListView listView;

	public Action<TypeDescription> OnSelect { get; set; }

	const string NoCategoryName = "Uncategorized";

	public ComponentTypeSelector( Widget parent ) : base( parent )
	{
		WindowTitle = "Select Component Type..";
		
		FixedWidth = 260;
		FixedHeight = 300;
		currentCategory = null;

		DeleteOnClose = true;

		listView = new ListView();

		listView.Margin = 0;
		listView.ItemSize = new Vector2( -1, 23 );
		listView.ItemSelected += x =>
		{
			if ( x is string category )
			{
				if ( currentCategory == category ) currentCategory = null;
				else currentCategory = category;
				UpdateItems();
				return;
			}

			if ( x is TypeDescription type )
			{
				OnSelect( type );
				Destroy();
			}
		};

		listView.ItemPaint = PaintItem;
		listView.OnPaintOverride = () =>
		{
			return false;
		};

		listView.ItemHoverEnter += ( o ) => Cursor = CursorShape.Finger;
		listView.ItemHoverLeave += ( o ) => Cursor = CursorShape.Arrow;

		//SearchField.ForwardNavigationEvents = listView;

		//OnSearchFilter = ( t ) =>
		//{
		//	searchString = t;
		//	UpdateItems();
		//};

		Layout = Layout.Column();
		Layout.Add( listView, 1 );
		Layout.Margin = 0;

		UpdateItems();
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;
		Paint.SetPen( Theme.WidgetBackground.Darken( 0.4f ), 1 );
		Paint.SetBrush( Theme.WidgetBackground );
		Paint.DrawRect( listView.LocalRect.Shrink( 1 ), 3 );
	}

	void UpdateItems()
	{
		listView.Clear();

		// entity components
		var types = EditorTypeLibrary.GetTypes<IPrefabObject.Component>().Where( x => !x.IsAbstract );

		// listView.SetItems( entityTypes.Where( x => x.Title.Contains( searchString, StringComparison.OrdinalIgnoreCase ) ) );

		if ( currentCategory  == null )
		{
			var categories = types.Select( x => string.IsNullOrWhiteSpace( x.Group ) ? NoCategoryName : x.Group ).Distinct().OrderBy( x => x ).ToArray();
			if ( categories.Length > 1 )
			{
				listView.SetItems( categories );
				return;
			}
		}
		else
		{
			types = types.Where( x => currentCategory == NoCategoryName ? x.Group == null : x.Group == currentCategory ).ToArray();
		}

		listView.AddItem( currentCategory );

		foreach ( var item in types.OrderBy( x => x.Title ) )
		{
			listView.AddItem( item );
		}
	}

	private void PaintItem( VirtualWidget obj )
	{
		bool highlight = obj.Hovered;
		Paint.SetPen( obj.GetForegroundColor() );

		if ( obj.Object is string curCat && curCat == currentCategory )
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.WidgetBackground.WithAlpha( highlight ? 0.7f : 0.4f ) );
			Paint.DrawRect( obj.Rect );

			var r = obj.Rect.Shrink( 12, 2 );
			Paint.SetPen( Theme.ControlText );
			Paint.DrawIcon( r, "arrow_back", 14, TextFlag.LeftCenter );

			Paint.SetDefaultFont( 8 );
			var t = Paint.DrawText( r, currentCategory, TextFlag.Center );
			return;
		}

		if ( obj.Object is string categoryTitle )
		{
			Paint.SetPen( Theme.ControlText.WithAlpha( highlight ? 1.0f : 0.5f ) );

			var r = obj.Rect.Shrink( 12, 2 );
			Paint.SetDefaultFont( 8 );
			Paint.DrawText( r, categoryTitle, TextFlag.LeftCenter );
			Paint.DrawIcon( r, "arrow_forward", 14, TextFlag.RightCenter );
			return;
		}

		if ( obj.Object is TypeDescription type )
		{
			
			var r = obj.Rect.Shrink( 12, 2 );

			Helpers.PaintComponentIcon( type, new Rect( r.Position, r.Height ).Shrink( 2 ), highlight ? 1.0f : 0.7f );
			r.Left += r.Height + 6;

			Paint.SetDefaultFont( 8 );
			Paint.SetPen( Theme.ControlText.WithAlpha( highlight ? 1.0f : 0.5f ) );
			var t = Paint.DrawText( r, type.Title, TextFlag.LeftCenter );

			//r.Left = t.Right + 8;

			//Paint.SetDefaultFont( );
			//Paint.SetPen( obj.GetForegroundColor().WithAlpha( 0.5f ) );
			//Paint.DrawText( r, $"{type.ClassName}, {type.FullName}", TextFlag.LeftBottom );
		}
	}
}
