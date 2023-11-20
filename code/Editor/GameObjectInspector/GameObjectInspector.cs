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

		var scroller = Layout.Add( new ScrollArea( this ) );
		scroller.Canvas = new Widget( scroller );
		scroller.Canvas.Layout = Layout.Column();

		if ( !target.IsPrefabInstance )
		{
			scroller.Canvas.Layout.Add( new ComponentList( target.Id, target.Components ) );

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
			if ( !target.IsPrefabInstanceRoot )
			{
				h.ReadOnly = true;
			}

			// if we're the prefab root, show a list of variables that can be modified

			// Add component button
			var row = scroller.Canvas.Layout.AddRow();
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

		scroller.Canvas.Layout.AddStretchCell( 1 );

		//var footer = scroller.Canvas.Layout.AddRow();
		//footer.Margin = 8;
		//footer.AddStretchCell();
		//var footerBtn = footer.Add( new Button.Primary( "Add Component", "add" ) );
		//footerBtn.Clicked = () => AddComponentDialog( footerBtn );
		//footer.Add( footerBtn );
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
	Guid GameObjectId;

	public ComponentList( Guid gameObjectId, List<BaseComponent> components ) : base( null )
	{
		GameObjectId = gameObjectId;
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
			var sheet = new ComponentSheet( GameObjectId, serialized, ( x ) => OpenContextMenu( o, x ) );
			Layout.Add( sheet );
			Layout.AddSeparator();
		}
	}

	void PropertyEdited( SerializedProperty property, BaseComponent component )
	{
		var value = property.GetValue<object>();
		component.EditLog( $"{component}.{property.Name}", component );
	}

	void OpenContextMenu( BaseComponent component, Vector2? position = null )
	{
		var menu = new Menu( this );

		menu.AddOption( "Reset", action: () => component.Reset() );
		menu.AddSeparator();

		var componentIndex = componentList.IndexOf( component );
		var canMoveUp = componentList.Count > 1 && componentIndex > 0;
		var canMoveDown = componentList.Count > 1 && componentIndex < componentList.Count - 1;

		menu.AddOption( "Move Up", action: () =>
		{
			componentList.RemoveAt( componentIndex );
			componentList.Insert( componentIndex - 1, component );
			Rebuild();
		} ).Enabled = canMoveUp;

		menu.AddOption( "Move Down", action: () =>
		{
			componentList.RemoveAt( componentIndex );
			componentList.Insert( componentIndex + 1, component );
			Rebuild();
		} ).Enabled = canMoveDown;

		menu.AddOption( "Remove Component", action: () => component.Destroy() );
		//menu.AddOption( "Copy To Clipboard" );
		//menu.AddOption( "Paste As New" );
		//menu.AddOption( "Paste Values" );
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
			menu.OpenAtCursor( true );
		}

	}

	int hashCode;

	[EditorEvent.Frame]
	public void Frame()
	{
		var hash = componentList?.Count ?? 0;

		if ( hashCode == hash ) return;

		hashCode = hash;
		Rebuild();
	}
}
