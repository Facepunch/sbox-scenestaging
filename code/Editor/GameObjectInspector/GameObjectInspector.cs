using Editor.EntityPrefabEditor;
using Sandbox;
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

		Layout.AddStretchCell();

		var footer = Layout.AddRow();
		footer.Margin = 8;
		footer.AddStretchCell();
		footer.Add( new Button.Primary( "Add Component", "add" ) { Clicked = AddComponentDialog } );
	}

	/// <summary>
	/// Pop up a window to add a component to this entity
	/// </summary>
	public void AddComponentDialog()
	{
		var s = new ComponentTypeSelector();

		s.OnSelectionFinished += t =>
		{
			TargetObject.AddComponent( t );
		};
		s.DoneButton.Text = "Add New Component";
		s.OpenBelowCursor( 16 );
	}
}

public class ComponentList : Widget
{
	List<GameObjectComponent> componentList; // todo - SerializedObject should support lists, arrays

	public ComponentList( List<GameObjectComponent> components ) : base( null )
	{
		componentList = components;
		Layout = Layout.Column();

		Rebuild();
	}

	void Rebuild()
	{
		Layout.Clear( true );

		foreach ( var o in componentList )
		{
			var serialized = EditorTypeLibrary.GetSerializedObject( o );
			var sheet = new ComponentSheet( serialized );
			Layout.Add( sheet );
			Layout.AddSeparator();
		}
	}
}
