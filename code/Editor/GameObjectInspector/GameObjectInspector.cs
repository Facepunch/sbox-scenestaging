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
		SerializedObject = TypeLibrary.GetSerializedObject( target );

		Layout = Layout.Column();

		var h = new GameObjectHeader( this, SerializedObject );

		Layout.Add( h );
		Layout.AddSeparator();
		Layout.Add( new ComponentList( target.Components ) );

		Layout.AddStretchCell();
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
			var serialized = TypeLibrary.GetSerializedObject( o );
			var sheet = new ComponentSheet( serialized );
			Layout.Add( sheet );
			Layout.AddSeparator();
		}
	}
}
