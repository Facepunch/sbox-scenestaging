using Sandbox.Clutter;

namespace Editor;

/// <summary>
/// Custom control widget for displaying Scatterer properties.
/// Shows all [Property] attributes from the scatterer instance.
/// </summary>
[CustomEditor( typeof(Scatterer), NamedEditor = "ScattererSettings" )]
public class ScattererSettingsControlWidget : ControlWidget
{
	private ControlSheet _sheet;
	private SerializedObject _serializedScatterer;
	private Scatterer _currentScatterer;

	public override bool SupportsMultiEdit => false;
	public override bool IncludeLabel => false;

	public ScattererSettingsControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Column();
		Layout.Spacing = 2;

		RebuildSheet();
	}

	private void RebuildSheet()
	{
		// Clear everything
		Layout.Clear( true );

		// Unsubscribe from old serialized object
		if ( _serializedScatterer != null )
		{
			_serializedScatterer.OnPropertyChanged -= OnScattererPropertyChanged;
		}

		_currentScatterer = SerializedProperty.GetValue<Scatterer>();
		if ( _currentScatterer == null )
		{
			Layout.Add( new Label( "No scatterer configured" ) );
			return;
		}

		// Create a new control sheet for the scatterer
		_sheet = new ControlSheet();

		// Get the serialized object for the scatterer
		_serializedScatterer = EditorTypeLibrary.GetSerializedObject( _currentScatterer );
		if ( _serializedScatterer != null )
		{
			// Subscribe to property changes
			_serializedScatterer.OnPropertyChanged += OnScattererPropertyChanged;

			// Add all properties from the scatterer
			foreach ( var prop in _serializedScatterer.Where( p => !p.HasAttribute<HideAttribute>() ) )
			{
				_sheet.AddRow( prop );
			}
		}

		Layout.Add( _sheet );
	}

	private void OnScattererPropertyChanged( SerializedProperty prop )
	{
		// When any scatterer property changes, notify the parent
		if ( SerializedProperty.Parent?.Targets?.FirstOrDefault() is ClutterDefinition clutterDefinition )
		{
			clutterDefinition.SaveScattererData();
		}
		SerializedProperty.Parent?.NoteChanged( SerializedProperty );
	}

	protected override void OnValueChanged()
	{
		base.OnValueChanged();

		// Only rebuild if the scatterer instance actually changed
		var newScatterer = SerializedProperty.GetValue<Scatterer>();
		if ( newScatterer != _currentScatterer )
		{
			RebuildSheet();
		}
	}
}
