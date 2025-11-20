using Editor;
using Sandbox;
using System.Linq;

namespace Editor;

/// <summary>
/// Custom control widget for displaying Scatterer properties.
/// Shows all [Property] attributes from the scatterer instance.
/// </summary>
[CustomEditor( typeof(Scatterer), NamedEditor = "ScattererSettings" )]
public class ScattererSettingsControlWidget : ControlWidget
{
	private ControlSheet _sheet;
	private string _lastSerializedState;

	public override bool SupportsMultiEdit => false;

	public ScattererSettingsControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Column();
		Layout.Spacing = 2;

		RebuildSheet();
		
		// Store initial state
		_lastSerializedState = SerializeScatterer();
	}

	private void RebuildSheet()
	{
		// Clear existing sheet
		_sheet?.Destroy();

		var scatterer = SerializedProperty.GetValue<Scatterer>();
		if ( scatterer == null )
		{
			var label = new Label( "No scatterer configured" );
			Layout.Add( label );
			return;
		}

		// Create a new control sheet for the scatterer
		_sheet = new ControlSheet();

		// Get the serialized object for the scatterer
		var serializedScatterer = EditorTypeLibrary.GetSerializedObject( scatterer );
		if ( serializedScatterer != null )
		{
			// Add all properties from the scatterer
			foreach ( var prop in serializedScatterer.Where( p => !p.HasAttribute<HideAttribute>() ) )
			{
				_sheet.AddRow( prop );
			}
		}

		Layout.Add( _sheet );
	}

	private string SerializeScatterer()
	{
		var scatterer = SerializedProperty.GetValue<Scatterer>();
		if ( scatterer == null ) return null;
		
		try
		{
			return Json.Serialize( scatterer );
		}
		catch
		{
			return null;
		}
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		// Check if the scatterer has changed
		var currentState = SerializeScatterer();
		if ( currentState != _lastSerializedState )
		{
			_lastSerializedState = currentState;
			
			// Trigger a change by re-setting the value
			var scatterer = SerializedProperty.GetValue<Scatterer>();
			SerializedProperty.SetValue( scatterer );
		}
	}
}
