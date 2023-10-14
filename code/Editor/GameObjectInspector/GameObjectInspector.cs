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
		SerializedObject.OnPropertyChanged += ( p ) => PropertyEdited( p, TargetObject );

		Layout = Layout.Column();

		var h = new GameObjectHeader( this, SerializedObject );

		Layout.Add( h );
		Layout.AddSeparator();

		if ( !target.IsPrefabInstance )
		{
			Layout.Add( new ComponentList( target.Components ) );

			// Add component button
			var row = Layout.AddRow();
			row.AddStretchCell();
			row.Margin = 16;
			var button = row.Add( new Button.Primary( "Add Component", "add" ) );
			button.MinimumWidth = 300;
			button.Clicked = () => AddComponentDialog( button );
			row.AddStretchCell();
		}
		else
		{
			if ( !target.IsPrefabInstanceRoot )
			{
				h.ReadOnly = true;
			}

			// if we're the prefab root, show a list of variables that can be modified

			// Add component button
			var row = Layout.AddRow();
			row.AddStretchCell();
			row.Margin = 16;
			var button = row.Add( new Button( $"Open \"{target.PrefabInstanceSource}\"", "edit" ) );

			button.Clicked = () =>
			{
				var prefabFile = target.PrefabInstanceSource;
				var asset = AssetSystem.FindByPath( prefabFile );
				asset.OpenInEditor();
			};
			row.AddStretchCell();
		}



		Layout.AddStretchCell();

		var footer = Layout.AddRow();
	////	footer.Margin = 8;
	//	footer.AddStretchCell();
	//	footer.Add( new Button.Primary( "Add Component", "add" ) { Clicked = AddComponentDialog } );
	}

	void PropertyEdited( SerializedProperty property, GameObject go )
	{
		var value = property.GetValue<object>();
		go.EditLog( $"{go.Name}.{property.Name}", go );
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
	List<BaseComponent> componentList; // todo - SerializedObject should support lists, arrays

	public ComponentList( List<BaseComponent> components ) : base( null )
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
			if ( o is null ) continue;

			var serialized = EditorTypeLibrary.GetSerializedObject( o );
			serialized.OnPropertyChanged += ( p ) => PropertyEdited( p, o );
			var sheet = new ComponentSheet( serialized, () => OpenContextMenu( o ) );
			Layout.Add( sheet );
			Layout.AddSeparator();
		}
	}

	void PropertyEdited( SerializedProperty property, BaseComponent component )
	{
		var value = property.GetValue<object>();
		component.EditLog( $"{component.Name}.{property.Name}", component );
	}

	void OpenContextMenu( BaseComponent component )
	{
		var menu = new Menu( this );

		menu.AddOption( "Reset", action: () => component.Reset() );
		menu.AddSeparator();
		menu.AddOption( "Remove Component", action: () => component.Destroy() );
		//menu.AddOption( "Copy To Clipboard" );
		//menu.AddOption( "Paste As New" );
		//menu.AddOption( "Paste Values" );
		//menu.AddOption( "Open In Window.." );
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
