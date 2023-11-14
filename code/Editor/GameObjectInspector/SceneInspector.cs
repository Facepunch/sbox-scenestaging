using Editor.EntityPrefabEditor;
using Sandbox;
using System;
using System.Collections.Generic;

namespace Editor.Inspectors;


[CanEdit( typeof(Scene) )]
public class SceneInspector : Widget
{
	SerializedObject SerializedObject;

	public SceneInspector( Widget parent, Scene target ) : base( parent )
	{
		SerializedObject = EditorTypeLibrary.GetSerializedObject( target );

		var cs = new ControlSheet();

		cs.AddRow( SerializedObject.GetProperty( nameof( Scene.Title ) ) );
		cs.AddRow( SerializedObject.GetProperty( nameof( Scene.Description ) ) );

		cs.AddRow( SerializedObject.GetProperty( nameof( Scene.TimeScale ) ) );
		cs.AddRow( SerializedObject.GetProperty( nameof( Scene.FixedUpdateFrequency ) ) );
		cs.AddRow( SerializedObject.GetProperty( nameof( Scene.ThreadedAnimation ) ) );
		cs.AddRow( SerializedObject.GetProperty( nameof( Scene.UseFixedUpdate ) ) );

		Layout = Layout.Column();
		Layout.Add( cs );
		Layout.AddStretchCell();
	}
}
