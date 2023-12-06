class GameObjectHeader : Widget
{
	SerializedObject Target;

	public GameObjectHeader( Widget parent, SerializedObject targetObject ) : base( parent )
	{
		Target = targetObject;

		Layout = Layout.Column();
		Layout.Margin = 0;
		Layout.Spacing = 0;

		// top section
		{
			var topRow = Layout.AddRow();
			topRow.Spacing = 4;
			topRow.Margin = 8;

			// big icon left
			{
				var left = topRow.AddRow();
				left.Add( new IconButton( "😃" ) 
				{ 
					FixedHeight = ControlWidget.ControlRowHeight * 2, 
					FixedWidth = ControlWidget.ControlRowHeight * 2,
					IconSize = 27,
					Background = Color.Transparent

				} );
			}

			// 2 rows right
			{
				var right = topRow.AddColumn();
				right.Spacing = 2;

				var top = right.AddRow();
				top.Spacing = 4;
				top.Add( new BoolControlWidget( targetObject.GetProperty( nameof( GameObject.Enabled ) ) ) { Icon = "power_settings_new", Color = Theme.Green } );
				top.Add( ControlWidget.Create( targetObject.GetProperty( nameof( GameObject.Name ) ) ), 1 );

				var bottom = right.AddRow();
				bottom.Spacing = 4;				
				bottom.Add( new BoolControlWidget( targetObject.GetProperty( nameof( GameObject.Networked ) ) ) { Icon = "wifi" } );
				bottom.Add( new BoolControlWidget( targetObject.GetProperty( nameof( GameObject.Lerping ) ) ) { Icon = "linear_scale" } );
				bottom.Add( ControlWidget.Create( targetObject.GetProperty( nameof( GameObject.Tags ) ) ) );
			}
		}

	//	Layout.AddSeparator();

		var cs = new ControlSheet();
		cs.Margin = new Sandbox.UI.Margin( 16, 0, 16, 8 );
		cs.SetMinimumColumnWidth( 0, 200 );
		cs.SetColumnStretch( 0, 1 );

		targetObject.GetProperty( nameof( GameObject.Transform ) ).TryGetAsObject( out var txo );

		cs.AddRow(  );
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

		var m = new Menu( this );

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

		m.OpenAtCursor( false );
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
