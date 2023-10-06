using Sandbox;
using Sandbox.Utility;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

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
		listView.ItemClicked += ItemSelect;
		listView.ItemActivated += ItemSelect;

		listView.ItemPaint = PaintItem;
		listView.OnPaintOverride = () =>
		{
			return false;
		};

		listView.ItemHoverEnter += ( o ) => Cursor = CursorShape.Finger;
		listView.ItemHoverLeave += ( o ) => Cursor = CursorShape.Arrow;

		var SearchField = new LineEdit( this );
		SearchField.MinimumHeight = 22;
		SearchField.PlaceholderText = "Search..";
		SearchField.ForwardNavigationEvents = listView;

		SearchField.TextEdited += ( t ) =>
		{
			searchString = t;
			UpdateItems();
		};

		Layout = Layout.Column();
		Layout.Add( SearchField );
		Layout.Add( listView, 1 );
		Layout.Margin = 0;

		UpdateItems();

		SearchField.Focus();
	}

	void ItemSelect( object x )
	{
		if ( x is string category )
		{
			// LOL what am I fucking doing
			if ( category == "__cc__" )
			{
				_ = CreateNewComponent();
				return;
			}

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
		var types = EditorTypeLibrary.GetTypes<BaseComponent>().Where( x => !x.IsAbstract );


		if ( !string.IsNullOrWhiteSpace( searchString ) )
		{
			listView.SetItems( types.Where( x => x.Title.Contains( searchString, StringComparison.OrdinalIgnoreCase ) ) );
			return;
		}

		if ( currentCategory == null )
		{
			var categories = types.Select( x => string.IsNullOrWhiteSpace( x.Group ) ? NoCategoryName : x.Group ).Distinct().OrderBy( x => x ).ToArray();
			if ( categories.Length > 1 )
			{
				listView.SetItems( categories );
				listView.AddItem( "__cc__" );
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

		obj.PaintBackground( Color.Transparent, 2.0f );

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
			// LOL what am I fucking doing
			if ( categoryTitle == "__cc__" )
			{
				var rect = obj.Rect.Shrink( 12, 2 );

				Paint.SetPen( Theme.Green.WithAlpha( highlight ? 1.0f : 0.7f ) );
				Paint.DrawIcon( new Rect( rect.Position, rect.Height ), "note_add", rect.Height, TextFlag.Center );
				rect.Left += rect.Height + 6;

				Paint.SetDefaultFont( 8 );
				Paint.SetPen( Theme.ControlText.WithAlpha( highlight ? 1.0f : 0.5f ) );
				Paint.DrawText( rect, "New Component..", TextFlag.LeftCenter );
				return;
			}

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

	/// <summary>
	/// We're creating a new component..
	/// </summary>
	async Task CreateNewComponent()
	{
		var codePath = LocalProject.CurrentGame.GetCodePath();

		var fd = new FileDialog( null );
		fd.Title = "Create new component..";
		fd.Directory = codePath;
		fd.DefaultSuffix = ".cs";
		fd.SelectFile( $"MyComponent.cs" );
		fd.SetFindFile();
		fd.SetModeSave();
		fd.SetNameFilter( "Cs File (*.cs)" );

		if ( !fd.Execute() )
			return;

		var componentName = System.IO.Path.GetFileNameWithoutExtension( fd.SelectedFile );

		if ( !System.IO.File.Exists( fd.SelectedFile ) )
		{
			var defaultComponent = $$"""
				using Sandbox;

				public sealed class {{componentName}} : GameObjectComponent
				{
					public override void Update()
					{
						
					}
				}

				""";

			System.IO.File.WriteAllText( fd.SelectedFile, defaultComponent );
		}

		// give it half a second, should do it
		await Task.Delay( 500 );

		// open it in the code editor
		CodeEditor.OpenFile( fd.SelectedFile );

		// we just wrote a file, lets wait until its compiled and loaded
		await EditorUtility.Projects.WaitForCompiles();

		var componentType = EditorTypeLibrary.GetType<BaseComponent>( componentName );
		if ( componentType is null )
		{
			Log.Warning( $"Couldn't find target component type {componentName}" );

			componentType = EditorTypeLibrary.GetType( componentName );
			Log.Warning( $"Couldn't find target component type {componentType}" );

			foreach ( var t in EditorTypeLibrary.GetTypes<BaseComponent>() )
			{
				Log.Info( $"{t}" );
			}
		}
		else
		{
			OnSelect( componentType );
		}

		Destroy();
	}
}
