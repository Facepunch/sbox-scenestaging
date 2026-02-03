using Sandbox.Clutter;

namespace Editor;

/// <summary>
/// Resource editor for ClutterDefinition using feature tabs.
/// </summary>
public class ClutterDefinitionEditor : BaseResourceEditor<ClutterDefinition>
{
	private ClutterDefinition _resource;
	private SerializedObject _serialized;
	private Widget _scattererContainer;
	private Layout _scattererLayout;

	protected override void Initialize( Asset asset, ClutterDefinition resource )
	{
		_resource = resource;

		Layout = Layout.Column();
		Layout.Spacing = 0;
		Layout.Margin = 0;

		_serialized = resource.GetSerialized();
		_serialized.OnPropertyChanged += OnPropertyChanged;

		var tabs = new TabWidget( this );
		tabs.VerticalSizeMode = SizeMode.CanGrow;
		tabs.AddPage( "Entries", "grass", CreateEntriesTab( _serialized ) );
		tabs.AddPage( "Scatterer", "scatter_plot", CreateScattererTab( _serialized, resource ) );
		tabs.AddPage( "Streaming", "grid_view", CreateStreamingTab( _serialized ) );

		Layout.Add( tabs, 1 );
	}

	private void OnPropertyChanged( SerializedProperty property )
	{
		_resource.StateHasChanged();

		if ( property.Name == nameof( ClutterDefinition.ScattererTypeName ) )
		{
			RebuildScattererSettings(); // Scatterer settings UI changed
		}
	}

	private void RebuildScattererSettings()
	{
		if ( _scattererLayout == null ) return;
		_scattererLayout.Clear( true );

		var scattererProperty = _serialized.GetProperty( nameof( ClutterDefinition.Scatterer ) );
		var scatterer = scattererProperty.GetValue<Scatterer>();
		if ( scatterer != null )
		{
			var sheet = new ControlSheet();
			var scattererSerialized = scatterer.GetSerialized();
			scattererSerialized.OnPropertyChanged += _ =>
			{
				_resource.StateHasChanged();
			};
			sheet.AddObject( scattererSerialized );
			_scattererLayout.Add( sheet, 1 );
		}
	}

	private Widget CreateEntriesTab( SerializedObject serialized )
	{
		var container = new Widget( null );
		container.Layout = Layout.Column();
		container.VerticalSizeMode = SizeMode.CanGrow;

		var sheet = new ControlSheet();
		var entriesProperty = serialized.GetProperty( nameof( ClutterDefinition.Entries ) );
		sheet.AddRow( entriesProperty );

		container.Layout.Add( sheet, 1 );
		return container;
	}

	private Widget CreateScattererTab( SerializedObject serialized, ClutterDefinition resource )
	{
		_scattererContainer = new Widget( null );
		_scattererContainer.Layout = Layout.Column();
		_scattererContainer.VerticalSizeMode = SizeMode.CanGrow;

		var sheet = new ControlSheet();

		var scattererTypeProperty = serialized.GetProperty( nameof( ClutterDefinition.ScattererTypeName ) );
		sheet.AddRow( scattererTypeProperty );

		_scattererContainer.Layout.Add( sheet );

		_scattererLayout = _scattererContainer.Layout.AddColumn();
		_scattererLayout.Spacing = 0;

		RebuildScattererSettings();

		return _scattererContainer;
	}

	private Widget CreateStreamingTab( SerializedObject serialized )
	{
		var container = new Widget( null );
		container.Layout = Layout.Column();
		container.VerticalSizeMode = SizeMode.CanGrow;

		var sheet = new ControlSheet();

		var tileSizeProperty = serialized.GetProperty( nameof( ClutterDefinition.TileSizeEnum ) );
		var tileRadiusProperty = serialized.GetProperty( nameof( ClutterDefinition.TileRadius ) );

		sheet.AddRow( tileSizeProperty );
		sheet.AddRow( tileRadiusProperty );

		container.Layout.Add( sheet, 1 );
		return container;
	}
}
