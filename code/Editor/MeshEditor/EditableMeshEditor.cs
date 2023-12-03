
[CanEdit( typeof( TestShit ) )]
public class MeshPartEditor : Widget
{

	TestShit Target;

	[Event( "scene.frame", Priority = 123 )]
	public void FrameToSelection( BBox bbox )
	{
		if ( Target?.Part == null ) return;
		if ( Target?.Mesh == null ) return;

		var center = Target.Mesh.CalculateCenter( new List<MeshPart>() { Target.Part } );
		center += Target.Transform.Position;
		bbox = new BBox( center, 64f );

		SceneViewWidget.Current.FrameOn( bbox );
	}

	public MeshPartEditor( Widget parent = null, TestShit target = null ) : base( parent )
	{
		Target = target;

		Layout = Layout.Column();
		Layout.Spacing = 4;

		var heading = Layout.Add( new Widget( this ) );
		heading.SetStyles( $"background-color:{Theme.ControlBackground.Hex}; padding: 4px 10px;" );
		heading.Layout = Layout.Row();

		var label = heading.Layout.Add( new Label( $"{target.Part.Type} operations" ) );

		if ( target.Part.Type == MeshPartTypes.Face )
		{
			var extrudeButton = Layout.Add( new Button.Clear( "Extrude" ) );
			extrudeButton.Clicked = () => target.Mesh.ExtrudeFace( target.Part, 64 );

			var makePlanar = Layout.Add( new Button.Clear( "Flatten" ) );
			makePlanar.Clicked = () => target.Mesh.FlattenFace( target.Part );
		}

		if ( target.Part.Type != MeshPartTypes.Vertex )
		{
			var deleteButton = Layout.Add( new Button.Clear( "Delete" ) );
			deleteButton.Clicked = () => target.Mesh.Delete( target.Part );
		}

		Layout.AddStretchCell( 1 );
	}

}

[CustomEditor( typeof( EditableMesh ) )]
public class EditableMeshEditor : ControlWidget
{

	public EditableMeshEditor( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Column();
		Layout.Spacing = 2;

		//var button = Layout.Add( new Button( "Extrude Selection" ) );
		//button.Pressed = () =>
		//{
		//	var mesh = property.GetValue<EditableMesh>( null );
		//	if ( mesh == null ) return;

		//	mesh.ExtrudeSelection( 64f );
		//};
	}

}
