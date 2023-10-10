
using Editor;
using Sandbox;
using System.Linq;

class GameObjectHeader : Widget
{
	SerializedObject Target;

	public GameObjectHeader( Widget parent, SerializedObject targetObject ) : base( parent )
	{
		Target = targetObject;
		GameObject parentEntity = targetObject.GetProperty( "Parent" ).GetValue<GameObject>();

		Layout = Layout.Column();
		Layout.Margin = 16;
		Layout.Spacing = 4;

		var row = Layout.AddRow();
		row.Spacing = 4;
		row.Add( ControlWidget.Create( targetObject.GetProperty( nameof( GameObject.Enabled ) ) ) );
		row.Add( ControlWidget.Create( targetObject.GetProperty( nameof( GameObject.Name ) ) ), 1 );

	//	if ( parentEntity is not null && parentEntity.Model is Model model )
	//	{
	//		row.Add( new AttachmentControlWidget( Target.GetProperty( "Attachment" ), model ) );
	//	}


		// this all needs cleaning up and formalizing


		// tag editor
		// todo: make a Tag type that json serializes from string or array etc
		// todo: make a TagControlWidget that uses SerializedProperty to edit
		{
			var w = new TagEdit( this );

			w.Convertors.Add( tag =>
			{
				if ( tag == null || tag.Length < 2 || tag.Length > 16 )
					return null;

				if ( !tag.All( char.IsLetterOrDigit ) )
					return null;

				tag = tag.ToLower();

				return new TagEdit.TagDetail
				{
					Value = tag,
					Title = tag,
				};
			} );

			var tags = targetObject.GetProperty( "Tags" );
			if ( tags is not null )
			{
				w.Bind( "Value" ).From( () => tags.As.String, x => tags.As.String = x );
			}
			Layout.Add( w );
		}

		var cs = new ControlSheet();
		cs.Margin = 0;
		cs.SetMinimumColumnWidth( 0, 50 );
		cs.SetColumnStretch( 0, 1 );

		targetObject.GetProperty( nameof( GameObject.Transform ) ).TryGetAsObject( out var txo );

		cs.AddRow( txo.GetProperty( nameof( GameTransform.LocalPosition ) ) );
		cs.AddRow( txo.GetProperty( nameof( GameTransform.LocalRotation ) ) );
		cs.AddRow( txo.GetProperty( nameof( GameTransform.LocalScale ) ) );

		Layout.Add( cs );

	}

	protected override void OnPaint()
	{
		base.OnPaint();

		Paint.ClearPen();
		Paint.SetBrush( Theme.WidgetBackground );
		Paint.DrawRect( LocalRect );
	}

}

class AttachmentControlWidget : ControlWidget
{
	Button selectionButton;

	public AttachmentControlWidget( SerializedProperty property, Model model ) : base( property )
	{
		ToolTip = "Attachment";
		Layout = Layout.Column();
		selectionButton = Layout.Add( new Button( "Attachment" ) { Clicked = () => OpenAttachmentSelector( model ) } );
		selectionButton.FixedHeight = MinimumHeight;
		UpdateButtonText();
	}

	protected override Vector2 SizeHint() => new Vector2( -1, -1 );

	void OpenAttachmentSelector( Model model )
	{
		// I hate this selector, we should make it a popup instead so you can
		// click away from it to cancel instead of finding a close button
		//var s = new AttachmentSelector( model, SerializedProperty );
		//s.DoneButton.Text = "Switch Attachment";
		//s.OnSelectionChanged = SerializedProperty.SetValue;
		//s.OnSelectionFinished = SerializedProperty.SetValue;
		//s.OpenBelowCursor( 16, 0.5f );
	}

	protected override void OnValueChanged()
	{
		base.OnValueChanged();
		UpdateButtonText();
	}

	void UpdateButtonText()
	{
		var txt = SerializedProperty.As.String;
		if ( txt == "_bonemerge" ) txt = "Bone Merge";
		if ( string.IsNullOrEmpty( txt ) ) txt = "Origin";
		selectionButton.Text = txt;
	}
}
