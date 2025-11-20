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

	public override bool SupportsMultiEdit => false;

	public ScattererSettingsControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Column();
		Layout.Spacing = 2;

		RebuildSheet();
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

	protected override void OnPaint()
	{
		base.OnPaint();

		// Check if scatterer type changed and rebuild if needed
		var currentScatterer = SerializedProperty.GetValue<Scatterer>();
		if ( currentScatterer != null && _sheet != null )
		{
			// Optionally refresh the sheet here if values change
		}
	}
}
