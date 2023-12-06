using Editor.EntityPrefabEditor;
using Sandbox.Utility;
using System;

namespace Editor.Inspectors;



[CanEdit( typeof( GameObject ) )]
[CanEdit( typeof( PrefabScene ) )]
public class GameObjectInspector : InspectorWidget
{
	public GameObjectInspector( SerializedObject so ) : base( so )
	{
		SerializedObject.OnPropertyChanged += ( p ) => PropertyEdited( p );

		Layout = Layout.Column();

		var h = new GameObjectHeader( this, SerializedObject );

		Layout.Add( h );
		Layout.AddSeparator();

		var scroller = Layout.Add( new ScrollArea( this ) );
		scroller.Canvas = new Widget( scroller );
		scroller.Canvas.Layout = Layout.Column();

		bool isPrefabInstance = SerializedObject.Targets.OfType<GameObject>().Any( x => x.IsPrefabInstance );


		if ( !isPrefabInstance )
		{
			scroller.Canvas.Layout.Add( new ComponentListWidget( SerializedObject ) );

			// Add component button
			var row = scroller.Canvas.Layout.AddRow();
			row.AddStretchCell();
			row.Margin = 16;
			var button = row.Add( new Button.Primary( "Add Component", "add" ) );
			button.MinimumWidth = 300;
			button.Clicked = () => AddComponentDialog( button );
			row.AddStretchCell();
		}
		else
		{
			//if ( !target.IsPrefabInstanceRoot )
			//{
			//	h.ReadOnly = true;
			//}

			// if we're the prefab root, show a list of variables that can be modified

			var source = SerializedObject.Targets.OfType<GameObject>().Select( x => x.PrefabInstanceSource ).FirstOrDefault();

			// Add component button
			var row = scroller.Canvas.Layout.AddRow();
			row.AddStretchCell();
			row.Margin = 16;
			var button = row.Add( new Button( $"Open \"{source}\"", "edit" ) );

			button.Clicked = () =>
			{
				var prefabFile = source;
				var asset = AssetSystem.FindByPath( prefabFile );
				asset.OpenInEditor();
			};
			row.AddStretchCell();
		}

		scroller.Canvas.Layout.AddStretchCell( 1 );
	}

	void PropertyEdited( SerializedProperty property )
	{
		//	var value = property.GetValue<object>();
		//	go.EditLog( $"{go.Name}.{property.Name}", go );
	}

	/// <summary>
	/// Pop up a window to add a component to this entity
	/// </summary>
	public void AddComponentDialog( Button source )
	{
		var s = new ComponentTypeSelector( this );
		s.OnSelect += ( t ) => AddComponent( t );
		s.OpenAt( source.ScreenRect.BottomLeft, animateOffset: new Vector2( 0, -4 ) );
		s.FixedWidth = source.Width;
	}

	private void AddComponent( TypeDescription componentType )
	{
		foreach( var go in SerializedObject.Targets.OfType<GameObject>() )
		{
			if ( go.IsPrefabInstance ) continue;

			go.Components.Create( componentType );
		}
	}

	private void PasteComponent()
	{
		foreach ( var go in SerializedObject.Targets.OfType<GameObject>() )
		{
			if ( go.IsPrefabInstance ) continue;

			Helpers.PasteComponentAsNew( go );
		}
	}

	protected override void OnContextMenu( ContextMenuEvent e )
	{
		if ( Helpers.HasComponentInClipboard() )
		{
			var menu = new Menu( this );
			menu.AddOption( "Paste Component As New", action: PasteComponent );
			menu.OpenAtCursor( false );
		}

		base.OnContextMenu( e );
	}
}

public class ComponentListWidget : Widget
{
	SerializedObject SerializedObject { get; init; }

	Guid GameObjectId;

	public ComponentListWidget( SerializedObject so ) : base( null )
	{
		SerializedObject = so;
		GameObjectId = so.IsMultipleTargets ? Guid.Empty : so.Targets.OfType<GameObject>().Select( x => x.Id ).Single();
		Layout = Layout.Column();
		Frame();
	}

	void Rebuild()
	{
		Layout.Clear( true );

		var gobs = SerializedObject.Targets.OfType<GameObject>().ToArray();
		if ( gobs.Length == 0 ) return;

		foreach ( var o in gobs[0].Components.GetAll() )
		{
			if ( o is null ) continue;

			var allGobs = gobs.Select( x => x.Components.GetAll().Where( y => y.GetType() == o.GetType() ).FirstOrDefault() ).ToArray();

			// Must be one on every go to show up
			if ( allGobs.Length != gobs.Length ) continue;
			if ( allGobs.Any( x => x is null ) ) continue;

			MultiSerializedObject mso = new MultiSerializedObject();

			foreach( var entry in allGobs )
			{
				var serialized = EditorTypeLibrary.GetSerializedObject( entry );
				mso.Add( serialized );
			}

			mso.OnPropertyChanged += ( p ) => PropertyEdited( p, o );
			var sheet = new ComponentSheet( GameObjectId, mso, ( x ) => OpenContextMenu( o, x ) );
			Layout.Add( sheet );
			Layout.AddSeparator();
		}
	}

	void PropertyEdited( SerializedProperty property, Component component )
	{
		var value = property.GetValue<object>();
		component.EditLog( $"{component}.{property.Name}", component );
	}

	void OpenContextMenu( Component component, Vector2? position = null )
	{
		if ( SerializedObject.IsMultipleTargets )
		{
			// TODO
			return;
		}

		var componentList = SerializedObject.Targets.OfType<GameObject>().Select( x => x.Components ).Single();

		var menu = new Menu( this );

		menu.AddOption( "Reset", action: () => component.Reset() );
		menu.AddSeparator();

		var componentIndex = componentList.GetAll().ToList().IndexOf( component );
		var canMoveUp = componentList.Count > 1 && componentIndex > 0;
		var canMoveDown = componentList.Count > 1 && componentIndex < componentList.Count - 1;

		menu.AddOption( "Move Up", action: () =>
		{
			componentList.Move( component, -1 );
			Rebuild();
		} ).Enabled = canMoveUp;

		menu.AddOption( "Move Down", action: () =>
		{
			componentList.Move( component, +1 );
			Rebuild();
		} ).Enabled = canMoveDown;

		menu.AddOption( "Remove Component", action: () =>
		{
			component.Destroy();
			SceneEditorSession.Active.Scene.EditLog( "Removed Component", component );
		} );
		menu.AddOption( "Copy To Clipboard", action: () => Helpers.CopyComponent( component ) );

		if ( Helpers.HasComponentInClipboard() )
		{
			menu.AddOption( "Paste Values", action: () => Helpers.PasteComponentValues( component ) );
			menu.AddOption( "Paste As New", action: () => Helpers.PasteComponentAsNew( component.GameObject ) );
		}

		//menu.AddOption( "Open In Window.." );
		menu.AddSeparator();

		var t = EditorTypeLibrary.GetType( component.GetType() );
		if ( t.SourceFile is not null )
		{
			var filename = System.IO.Path.GetFileName( t.SourceFile );
			menu.AddOption( $"Open {filename}..", action: () => CodeEditor.OpenFile( t.SourceFile, t.SourceLine ) );
		}

		if ( position != null )
		{
			menu.OpenAt( position.Value, true );
		}
		else
		{
			menu.OpenAtCursor( false );
		}

	}


	[EditorEvent.Frame]
	public void Frame()
	{
		int hash = 1;

		hash = SerializedObject.Targets.OfType<GameObject>().Sum( x => x.Components.Count );

		if ( !SetContentHash( hash ) )
			return;

		Rebuild();
	}
}
