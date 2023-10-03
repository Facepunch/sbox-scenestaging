using Editor.EntityPrefabEditor;
using Sandbox;
using System;
using System.Collections.Generic;

namespace Editor.Inspectors;


[CanEdit( typeof(GameObject) )]
public class GameObjectInspector : Widget
{
	GameObject TargetObject;
	SerializedObject SerializedObject;

	public GameObjectInspector( Widget parent, GameObject target ) : base( parent )
	{
		TargetObject = target;
		SerializedObject = EditorTypeLibrary.GetSerializedObject( target );

		Layout = Layout.Column();

		var h = new GameObjectHeader( this, SerializedObject );

		Layout.Add( h );
		Layout.AddSeparator();
		Layout.Add( new ComponentList( target.Components ) );

		//
		// Add component
		//
		{
			var row = Layout.AddRow();
			row.AddStretchCell();
			row.Margin = 16;
			var button = row.Add( new Button( "Add Component", "add" ) );
			button.FixedWidth = 230;
			button.Clicked = () => AddComponentDialog( button );
			row.AddStretchCell();
		}

		Layout.AddStretchCell();

		var footer = Layout.AddRow();
	////	footer.Margin = 8;
	//	footer.AddStretchCell();
	//	footer.Add( new Button.Primary( "Add Component", "add" ) { Clicked = AddComponentDialog } );
	}

	/// <summary>
	/// Pop up a window to add a component to this entity
	/// </summary>
	public void AddComponentDialog( Button source )
	{
		var s = new ComponentTypeSelector( this );
		s.OnSelect += ( t ) => TargetObject.AddComponent( t );
		s.OpenAt( source.ScreenRect.BottomLeft, animateOffset: new Vector2( 0, -4 ) );
		s.FixedWidth = source.Width;
	}
}

public class ComponentList : Widget
{
	List<GameObjectComponent> componentList; // todo - SerializedObject should support lists, arrays

	public ComponentList( List<GameObjectComponent> components ) : base( null )
	{
		componentList = components;
		Layout = Layout.Column();

		hashCode = -1;
		Frame();
	}

	void Rebuild()
	{
		Layout.Clear( true );

		foreach ( var o in componentList )
		{
			var serialized = EditorTypeLibrary.GetSerializedObject( o );
			var sheet = new ComponentSheet( serialized, () => OpenContextMenu( o ) );
			Layout.Add( sheet );
			Layout.AddSeparator();
		}
	}

	void OpenContextMenu( GameObjectComponent component )
	{
		var menu = new Menu( this );

		menu.AddOption( "Reset", action: () => component.Reset() );
		menu.AddSeparator();
		menu.AddOption( "Remove Component", action: () => component.Destroy() );
		menu.AddOption( "Copy To Clipboard" );
		menu.AddOption( "Paste As New" );
		menu.AddOption( "Paste Values" );
		menu.AddOption( "Open In Window.." );
		menu.AddSeparator();

		var t = EditorTypeLibrary.GetType( component.GetType() );
		if ( t.SourceFile is not null )
		{
			Log.Info( component.GetType() );
			Log.Info( t.FullName );
			Log.Info( t.SourceFile );

			var filename = System.IO.Path.GetFileName( t.SourceFile );
			menu.AddOption( $"Open {filename}..", action: () => CodeEditor.OpenFile( t.SourceFile, t.SourceLine ) );
		}

		menu.OpenAtCursor();

	}

	int hashCode;

	[EditorEvent.Frame]
	public void Frame()
	{
		var hash = componentList.Count;

		if ( hashCode == hash ) return;

		hashCode = hash;
		Rebuild();
	}
}
