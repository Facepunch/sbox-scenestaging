using Sandbox.Utility;
using System;
using System.Threading.Tasks;

namespace Editor.EntityPrefabEditor;

/// <summary>
/// A popup dialog to select an entity type
/// </summary>
internal partial class ComponentTypeSelector : PopupWidget
{
	public Action<TypeDescription> OnSelect { get; set; }
	List<ComponentSelection> Panels { get; set; } = new();
	int CurrentPanelId { get; set; } = 0;
	Widget Main { get; set; }

	string searchString;
	const string NoCategoryName = "Uncategorized";

	internal LineEdit Search { get; init; }

	public ComponentTypeSelector( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();

		var head = Layout.Row();
		head.Margin = 6;

		Layout.Add( head );

		Main = new Widget( this );
		Main.Layout = Layout.Row();
		Layout.Add( Main );

		FixedWidth = 260;
		MaximumHeight = 300;
		DeleteOnClose = true;

		Search = new LineEdit( this );
		Search.MinimumHeight = 22;
		Search.PlaceholderText = "Search..";
		Search.TextEdited += ( t ) =>
		{
			searchString = t;
			ResetSelection();
		};

		head.Add( Search );

		ResetSelection();

		Search.Focus();
	}

	/// <summary>
	/// Pushes a new selection to the selector
	/// </summary>
	/// <param name="selection"></param>
	void PushSelection( ComponentSelection selection )
	{
		CurrentPanelId++;

		// Do we have something at our new index, if so, kill it
		if ( Panels.Count > CurrentPanelId && Panels.ElementAt( CurrentPanelId ) is var existingObj ) existingObj.Destroy();

		Panels.Insert( CurrentPanelId, selection );
		Main.Layout.Add( selection, 1 );

		UpdateSelection( selection );
		AnimateSelection( true, Panels[CurrentPanelId - 1], selection );
	}

	/// <summary>
	/// Pops the current selection off
	/// </summary>
	internal void PopSelection()
	{
		// Don't pop while empty
		if ( CurrentPanelId == 0 ) return;

		var currentIdx = Panels[CurrentPanelId];
		CurrentPanelId--;

		AnimateSelection( false, currentIdx, Panels[CurrentPanelId] );
	}

	/// <summary>
	/// Runs an animation on the last selection, and the current selection.
	/// I kinda hate this. A lot. But it's pretty.
	/// </summary>
	/// <param name="forward"></param>
	/// <param name="prev"></param>
	/// <param name="selection"></param>
	void AnimateSelection( bool forward, ComponentSelection prev, ComponentSelection selection )
	{
		const string easing = "ease-out";
		const float speed = 0.3f;

		var distance = Width;

		var prevFrom = prev.Position.x;
		var prevTo = forward ? prev.Position.x - distance : prev.Position.x + distance;

		var selectionFrom = forward ? selection.Position.x + distance : selection.Position.x;
		var selectionTo = forward ? selection.Position.x : selection.Position.x + distance;

		var func = ( ComponentSelection a, float x ) =>
		{
			a.Position = a.Position.WithX( x );
			OnMoved();
		};

		Animate.Add( prev, speed, prevFrom, prevTo, x => func( prev, x ), easing );
		Animate.Add( selection, speed, selectionFrom, selectionTo, x => func( selection, x ), easing );
	}

	/// <summary>
	/// Resets the current selection, useful when setting up / searching
	/// </summary>
	protected void ResetSelection()
	{
		Main.Layout.Clear( true );
		Panels.Clear();

		var selection = new ComponentSelection( this, this );

		CurrentPanelId = 0;

		UpdateSelection( selection );

		Panels.Add( selection );
		Main.Layout.Add( selection );
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;
		Paint.SetPen( Theme.WidgetBackground.Darken( 0.4f ), 1 );
		Paint.SetBrush( Theme.WidgetBackground );
		Paint.DrawRect( LocalRect.Shrink( 1 ), 3 );
	}

	/// <summary>
	/// Called when a category is selected
	/// </summary>
	/// <param name="category"></param>
	void OnCategorySelected( string category )
	{
		// Push this as a new selection
		PushSelection( new ComponentSelection( this, this, category ) );
	}

	/// <summary>
	/// Called when an individual component is selected
	/// </summary>
	/// <param name="type"></param>
	void OnComponentSelected( TypeDescription type )
	{
		OnSelect( type );
		Destroy();
	}

	/// <summary>
	/// Called when the New Component button is pressed
	/// </summary>
	void OnNewComponentSelected( string componentName = "MyComponent" )
	{
		_ = CreateNewComponent( componentName );
		Destroy();
	}

	protected override void OnKeyRelease( KeyEvent e )
	{
		if ( e.Key == KeyCode.Down )
		{
			var selection = Panels[CurrentPanelId];
			if ( selection.ItemList.FirstOrDefault() != null )
			{
				selection.Focus();
				e.Accepted = true;
			}
		}
	}

	/// <summary>
	/// Updates any selection
	/// </summary>
	/// <param name="selection"></param>
	void UpdateSelection( ComponentSelection selection )
	{
		selection.Clear();

		selection.ItemList.Add( selection.CategoryHeader );

		// entity components
		var types = EditorTypeLibrary.GetTypes<BaseComponent>().Where( x => !x.IsAbstract );

		if ( !string.IsNullOrWhiteSpace( searchString ) )
		{
			var query = types.Where( x => x.Title.Contains( searchString, StringComparison.OrdinalIgnoreCase ) );
			foreach ( var type in query )
			{
				selection.AddEntry( new ComponentEntry( selection, type ) { MouseClick = () => OnComponentSelected( type ) } );
			}

			selection.AddEntry( new ComponentEntry( selection ) { Text = $"New '{searchString}' Component...", MouseClick = () => OnNewComponentSelected( searchString ) } );
			selection.AddStretchCell();
			return;
		}

		if ( selection.Category == null )
		{
			var categories = types.Select( x => string.IsNullOrWhiteSpace( x.Group ) ? NoCategoryName : x.Group ).Distinct().OrderBy( x => x ).ToArray();
			if ( categories.Length > 1 )
			{
				foreach ( var category in categories )
				{
					selection.AddEntry( new ComponentCategory( selection )
					{
						Category = category,
						MouseClick = () => OnCategorySelected( category ),
					} );
				}

				selection.AddEntry( new ComponentEntry( selection ) { Text = "New Component...", MouseClick = () => OnNewComponentSelected() } );
				selection.AddStretchCell();

				return;
			}
		}
		else
		{
			types = types.Where( x => selection.Category == NoCategoryName ? x.Group == null : x.Group == selection.Category ).OrderBy( x => x.Title );

			foreach ( var type in types )
			{
				selection.AddEntry( new ComponentEntry( selection, type ) { MouseClick = () => OnComponentSelected( type ) } );
			}
			selection.AddStretchCell();
		}
	}

	/// <summary>
	/// We're creating a new component..
	/// </summary>
	async Task CreateNewComponent( string componentName = "MyComponent" )
	{
		var codePath = LocalProject.CurrentGame.GetCodePath();

		var fd = new FileDialog( null );
		fd.Title = "Create new component..";
		fd.Directory = codePath;
		fd.DefaultSuffix = ".cs";
		fd.SelectFile( $"{componentName}.cs" );
		fd.SetFindFile();
		fd.SetModeSave();
		fd.SetNameFilter( "Cs File (*.cs)" );

		if ( !fd.Execute() )
			return;
		
		// User might change their mind on the component name
		componentName = System.IO.Path.GetFileNameWithoutExtension( fd.SelectedFile );

		if ( !System.IO.File.Exists( fd.SelectedFile ) )
		{
			var defaultComponent = $$"""
				using Sandbox;

				public sealed class {{componentName}} : BaseComponent
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

	/// <summary>
	/// A widget that contains a given selection - we hold this in a class because more than one can exist.
	/// </summary>
	partial class ComponentSelection : Widget
	{
		internal string Category { get; init; }
		internal Widget CategoryHeader { get; init; }
		ScrollArea Scroller { get; init; }
		ComponentTypeSelector Selector { get; set; }

		internal List<Widget> ItemList { get; private set; } = new();
		internal int CurrentItemId { get; private set; } = 0;
		internal Widget CurrentItem { get; private set; }

		internal ComponentSelection( Widget parent, ComponentTypeSelector selector, string categoryName = null ) : base( parent )
		{
			Selector = selector;
			Category = categoryName;
			FixedWidth = 300;
			MaximumHeight = 220;

			Layout = Layout.Column();

			CategoryHeader = new Widget( this );
			CategoryHeader.FixedHeight = 24;
			CategoryHeader.OnPaintOverride = PaintHeader;
			CategoryHeader.MouseClick = Selector.PopSelection;
			Layout.Add( CategoryHeader );

			Scroller = new ScrollArea( this );
			Scroller.Layout = Layout.Column();
			Layout.Add( Scroller, 1 );

			Scroller.Canvas = new Widget( Scroller );
			Scroller.Canvas.Layout = Layout.Column();
		}

		protected bool SelectMoveRow( int delta )
		{
			var selection = Selector.Panels[Selector.CurrentPanelId];
			if ( delta == 1 && selection.ItemList.Count - 1 > selection.CurrentItemId )
			{
				selection.CurrentItem = selection.ItemList[++selection.CurrentItemId];
				selection.Update();

				return true;
			}
			else if ( delta == -1 )
			{
				if ( selection.CurrentItemId > 0 )
				{
					selection.CurrentItem = selection.ItemList[--selection.CurrentItemId];
					selection.Update();

					return true;
				}
				else
				{
					selection.Selector.Search.Focus();
					selection.CurrentItem = null;
					selection.Update();
					return true;
				}
			}

			return false;
		}

		protected bool Enter()
		{
			var selection = Selector.Panels[Selector.CurrentPanelId];
			if ( selection.ItemList[selection.CurrentItemId] is Widget entry )
			{
				entry.MouseClick?.Invoke();
				return true;
			}

			return false;
		}

		protected override void OnKeyRelease( KeyEvent e )
		{
			// Move down
			if ( e.Key == KeyCode.Down && SelectMoveRow( 1 ) )
			{
				e.Accepted = true;
				return;
			}

			// Move up 
			if ( e.Key == KeyCode.Up && SelectMoveRow( -1 ) )
			{
				e.Accepted = true;
				return;
			}

			// Back button while in any selection, goes to previous selction.
			if ( e.Key == KeyCode.Left )
			{
				e.Accepted = true;
				Selector.PopSelection();
				return;
			}

			// Moving right, or hitting the enter key assumes you're trying to select something
			if ( ( e.Key == KeyCode.Return || e.Key == KeyCode.Right ) && Enter() )
			{
				e.Accepted = true;
				return;
			}
		}

		internal bool PaintHeader()
		{
			var c = CategoryHeader;
			var selected = c.IsUnderMouse || CurrentItem == c;

			Paint.ClearPen();
			Paint.SetBrush( selected ? Theme.Selection : Theme.WidgetBackground.WithAlpha( selected ? 0.7f : 0.4f ) );
			Paint.DrawRect( c.LocalRect );

			var r = c.LocalRect.Shrink( 12, 2 );
			Paint.SetPen( Theme.ControlText );

			if ( Selector.CurrentPanelId > 0 )
			{
				Paint.DrawIcon( r, "arrow_back", 14, TextFlag.LeftCenter );
			}

			Paint.SetDefaultFont( 8 );
			Paint.DrawText( r, string.IsNullOrEmpty( Category ) ? "Component" : Category, TextFlag.Center );

			return true;
		}

		/// <summary>
		/// Adds a new entry to the current selection.
		/// </summary>
		/// <param name="entry"></param>
		internal void AddEntry( ComponentBaseEntry entry )
		{
			Scroller.Canvas.Layout.Add( entry );
			ItemList.Add( entry );
			entry.Selector = this;
		}

		/// <summary>
		/// Adds a stretch cell to the bottom of the selection - good to call this when you know you're done adding entries.
		/// </summary>
		internal void AddStretchCell()
		{
			Scroller.Canvas.Layout.AddStretchCell( 1 );
			Update();
		}

		/// <summary>
		/// Clears the current selection
		/// </summary>
		internal void Clear()
		{
			Scroller.Canvas.Layout.Clear( true );
			ItemList.Clear();
		}

		protected override void OnPaint()
		{
			Paint.Antialiasing = true;
			Paint.SetPen( Theme.WidgetBackground.Darken( 0.8f ), 1 );
			Paint.SetBrush( Theme.WidgetBackground.Darken( 0.2f ) );
			Paint.DrawRect( LocalRect.Shrink( 0 ), 3 );
		}
	}

	/// <summary>
	/// All component entries are derived from this..
	/// </summary>
	abstract class ComponentBaseEntry : Widget
	{
		internal ComponentSelection Selector { get; set; }

		internal ComponentBaseEntry( Widget parent ) : base( parent )
		{
			FixedHeight = 24;
		}
	}

	/// <summary>
	/// A component entry
	/// </summary>
	class ComponentEntry : ComponentBaseEntry
	{
		public string Text { get; set; } = "My Component";
		public string Icon { get; set; } = "note_add";

		public TypeDescription Type { get; init; }

		internal ComponentEntry( Widget parent, TypeDescription type = null ) : base( parent )
		{
			Type = type;

			if ( type is not null )
			{
				Text = type.Title;
				Icon = type.Icon;
			}
		}

		protected override void OnPaint()
		{
			var r = LocalRect.Shrink( 12, 2 );
			var selected = IsUnderMouse || Selector.CurrentItem == this;
			var opacity = selected ? 1.0f : 0.7f;

			if ( selected )
			{
				Paint.ClearPen();
				Paint.SetBrush( Theme.Selection );
				Paint.DrawRect( LocalRect );
			}

			if ( Type is not null && !string.IsNullOrEmpty( Type.Icon ) )
			{
				Helpers.PaintComponentIcon( Type, new Rect( r.Position, r.Height ).Shrink( 2 ), opacity );
			}
			else
			{
				Paint.SetPen( Theme.Green.WithAlpha( opacity ) );
				Paint.DrawIcon( new Rect( r.Position, r.Height ).Shrink( 2 ), "note_add", r.Height, TextFlag.Center );
			}

			r.Left += r.Height + 6;

			Paint.SetDefaultFont( 8 );
			Paint.SetPen( Theme.ControlText.WithAlpha( selected ? 1.0f : 0.5f ) );
			Paint.DrawText( r, Text, TextFlag.LeftCenter );
		}
	}

	/// <summary>
	/// A category component entry
	/// </summary>
	class ComponentCategory : ComponentBaseEntry
	{
		public string Category { get; set; }
		public ComponentCategory( Widget parent ) : base( parent ) { }

		protected override void OnPaint()
		{
			var selected = IsUnderMouse || Selector.CurrentItem == this;

			if ( selected )
			{
				Paint.ClearPen();
				Paint.SetBrush( Theme.Selection );
				Paint.DrawRect( LocalRect );
			}

			var r = LocalRect.Shrink( 12, 2 );

			Paint.SetPen( Theme.ControlText.WithAlpha( selected ? 1.0f : 0.5f ) );

			Paint.SetDefaultFont( 8 );
			Paint.DrawText( r, Category, TextFlag.LeftCenter );
			Paint.DrawIcon( r, "arrow_forward", 14, TextFlag.RightCenter );
		}
	}
}
