namespace Editor;

/// <summary>
/// Custom editor widget for the clutter volume component.
/// Handles all scattering and clearing logic.
/// </summary>
[CustomEditor( typeof( ClutterVolumeComponent ) )]
partial class ClutterVolumeComponentWidget : ComponentEditorWidget
{
	ControlWidget scattererControlWidget;
	public ClutterVolumeComponent Volume => SerializedObject.Targets.FirstOrDefault() as ClutterVolumeComponent;

	public ClutterVolumeComponentWidget( SerializedObject obj ) : base( obj )
	{
		SetSizeMode( SizeMode.Default, SizeMode.Default );

		Layout = Layout.Column();
		BuildUI();
	}

	void BuildUI()
	{
		Layout.Clear( true );

		var sheet = new ControlSheet
		{
			Margin = 8
		};

		// Add basic properties
		sheet.AddRow( SerializedObject.GetProperty( nameof( ClutterVolumeComponent.SelectedLayers ) ) );

		// Add ScattererName with settings button
		scattererControlWidget = sheet.AddRow( SerializedObject.GetProperty( nameof( ClutterVolumeComponent.ScattererName ) ) );

		if ( Volume != null && !string.IsNullOrEmpty( Volume.ScattererName ) && scattererControlWidget != null )
		{
			var settingsButton = new IconButton( "settings" )
			{
				ToolTip = "Configure Scatterer Settings",
				OnClick = () => OpenScattererSettings( Volume ),
				FixedWidth = 20,
				FixedHeight = 20
			};
			scattererControlWidget.Layout.Add( settingsButton );
		}
		sheet.AddRow( SerializedObject.GetProperty( nameof( ClutterVolumeComponent.Density ) ) );
		sheet.AddRow( SerializedObject.GetProperty( nameof( ClutterVolumeComponent.Scale ) ) );
		Layout.Add( sheet );

		// Scatter
		var layerActionsRow = Layout.AddRow();
		layerActionsRow.Margin = 8;
		layerActionsRow.Spacing = 8;
		layerActionsRow.AddStretchCell();

		var scatterButton = new Button( "Scatter", "scatter_plot" )
		{
			Clicked = () =>
			{
				var clutterSystem = Volume.Scene.GetSystem<ClutterSystem>();
				clutterSystem.QueueScatterRequest( () =>
				{
					// Scatter using the volume's settings
					new ClutterScatterer( Volume.Scene )
						.WithVolume( Volume )
						.WithClear( true )
						.Run();
				} );
			},
			MaximumWidth = 200,
		};
		layerActionsRow.Add( scatterButton );

		// Clear
		var clearButton = new Button( "Clear", "delete" )
		{
			Clicked = () => 
			{
				var clutterSystem = Volume.Scene.GetSystem<ClutterSystem>();
				clutterSystem.ClearVolume( Volume );
			},
			MaximumWidth = 200
		};
		layerActionsRow.Add( clearButton );

		Layout.Add( layerActionsRow, 0);
	}

	void OpenScattererSettings( ClutterVolumeComponent volume )
	{
		if ( volume?.Scatterer == null )
		{
			Log.Warning( "No scatterer instance available" );
			return;
		}

		// Ensure settings are loaded from JSON before opening dialog
		volume.EnsureScattererSettingsLoaded();

		// Create a dialog window
		var dialog = new Widget( null )
		{
			WindowFlags = WindowFlags.Dialog,
			WindowTitle = $"{volume.ScattererName} Settings",
			DeleteOnClose = true,
			MinimumSize = new Vector2( 400, 300 ),

			Layout = Layout.Column()
		};
		dialog.Layout.Margin = 16;
		dialog.Layout.Spacing = 8;

		// Serialize the scatterer directly using TypeLibrary
		var scattererSo = volume.Scatterer.GetSerialized();
		var settingsSheet = new ControlSheet();
		settingsSheet.AddObject( scattererSo );

		dialog.Layout.Add( settingsSheet, 1 );

		// Add OK/Cancel buttons
		var buttonLayout = Layout.Row();
		buttonLayout.Spacing = 8;
		buttonLayout.AddStretchCell();

		var okButton = new Button( "OK" )
		{
			Clicked = () =>
			{
				// Serialize the scatterer settings before closing
				volume.SerializeScattererSettings();
				dialog.Close();
			}
		};

		var cancelButton = new Button( "Cancel" );
		cancelButton.Clicked = () => dialog.Close();

		buttonLayout.Add( okButton );
		buttonLayout.Add( cancelButton );
		dialog.Layout.Add( buttonLayout );

		dialog.Visible = true;
		dialog.Position = scattererControlWidget?.ScreenRect.BottomLeft ?? Application.CursorPosition;
		dialog.ConstrainToScreen();
	}
}
