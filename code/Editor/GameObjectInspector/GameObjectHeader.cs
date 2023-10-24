class GameObjectHeader : Widget
{
	SerializedObject Target;

	public GameObjectHeader( Widget parent, SerializedObject targetObject ) : base( parent )
	{
		Target = targetObject;
		GameObject parentEntity = targetObject.GetProperty( "Parent" ).GetValue<GameObject>();

		Layout = Layout.Column();
		Layout.Margin = 16;
		Layout.Spacing = 2;

		var row = Layout.AddRow();
		row.Spacing = 4;
		row.Add( ControlWidget.Create( targetObject.GetProperty( nameof( GameObject.Enabled ) ) ) );
		row.Add( ControlWidget.Create( targetObject.GetProperty( nameof( GameObject.Name ) ) ), 1 );

		var cs = new ControlSheet();
		cs.Margin = 0;
		cs.SetMinimumColumnWidth( 0, 75 );
		cs.SetColumnStretch( 0, 1 );

		targetObject.GetProperty( nameof( GameObject.Transform ) ).TryGetAsObject( out var txo );

		cs.AddRow( targetObject.GetProperty( nameof( GameObject.Tags ) ) );
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

	protected override void OnContextMenu( ContextMenuEvent e )
	{
		e.Accepted = true;

		var m = new Menu();

		var clipText = EditorUtility.Clipboard.Paste();

		try
		{
			var tx = Json.Deserialize<Transform>( clipText );
			if ( tx != default )
			{
				m.AddOption( "Paste Transform", action: () =>
				{
					Target.GetProperty( "Transform" ).TryGetAsObject( out var txo );
					txo.GetProperty( "Local" ).SetValue( tx );
					SceneEditorSession.Active.FullUndoSnapshot( "Paste Transform" );
				} );

				m.AddSeparator();
			}
		}
		catch ( System.Exception )
		{
			// ignore
		}

		m.AddOption( "Reset Transform", action: () =>
		{
			Target.GetProperty( "Transform" ).TryGetAsObject( out var txo );
			txo.GetProperty( "Local" ).SetValue( Transform.Zero );

			SceneEditorSession.Active.FullUndoSnapshot( "Reset Transform" );
		} );

		m.AddSeparator();

		m.AddOption( "Copy Transform", action: () =>
		{
			Target.GetProperty( "Transform" ).TryGetAsObject( out var txo );
			var tx = txo.GetProperty( "Local" ).GetValue<Transform>();

			EditorUtility.Clipboard.Copy( Json.Serialize( tx ) );
		} );

		m.OpenAtCursor();
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
