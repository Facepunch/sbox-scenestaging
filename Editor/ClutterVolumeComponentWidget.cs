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

		// Add ScattererName with settings button outside the sheet
		var scattererRow = Layout.AddRow();
		scattererRow.Margin = new Sandbox.UI.Margin( 8, 8, 8, 0 );
		scattererRow.Spacing = 0;

		var scattererSheet = new ControlSheet();
		scattererSheet.Margin = 0;
		scattererControlWidget = scattererSheet.AddRow( SerializedObject.GetProperty( nameof( ClutterVolumeComponent.ScattererName ) ) );
		scattererRow.Add( scattererSheet, 1 );

		if ( Volume != null && !string.IsNullOrEmpty( Volume.ScattererName ) )
		{
			var settingsButton = new IconButton( "settings" )
			{
				ToolTip = "Configure Scatterer Settings",
				OnClick = () => OpenScattererSettings( Volume ),
				FixedWidth = 24,
				FixedHeight = 24
			};
			scattererRow.Add( settingsButton );
		}

		// Add other properties in their own sheet
		var sheet = new ControlSheet
		{
			Margin = new Sandbox.UI.Margin( 8, 0, 8, 8 )
		};
		sheet.AddRow( SerializedObject.GetProperty( nameof( ClutterVolumeComponent.SelectedLayers ) ) );
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
			MinimumWidth = 250,
			MaximumWidth = 350,

			Layout = Layout.Column()
		};
		dialog.Layout.Margin = 8;
		dialog.Layout.Spacing = 4;
		dialog.SetStyles( "background-color: #2b2b2b;" );

		// Serialize the scatterer directly using TypeLibrary
		var scattererSo = volume.Scatterer.GetSerialized();
		var settingsSheet = new ControlSheet();
		settingsSheet.Margin = 8;
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
