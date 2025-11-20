using Editor;
using Sandbox;
using System;
using System.Linq;

namespace Editor;

/// <summary>
/// Custom control widget for the ScattererTypeName property in ClutterIsotope.
/// Provides a dropdown/combobox to select scatterer types.
/// </summary>
[CustomEditor( typeof(string), NamedEditor = "ScattererTypeSelector" )]
public class ScattererTypeControlWidget : ControlWidget
{
	private ComboBox _comboBox;

	public override bool SupportsMultiEdit => false;

	public ScattererTypeControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Row();
		Layout.Spacing = 4;

		// Create the combobox
		_comboBox = new ComboBox( this );
		_comboBox.Editable = false;

		// Populate it with scatterer types
		RebuildComboBox();

		Layout.Add( _comboBox );

		// Listen for changes
		_comboBox.TextChanged += () =>
		{
			SerializedProperty.SetValue( _comboBox.CurrentText );
		};
	}

	private void RebuildComboBox()
	{
		var currentValue = SerializedProperty.GetValue<string>();
		
		if ( string.IsNullOrEmpty( currentValue ) )
		{
			currentValue = "SimpleScatterer";
		}

		_comboBox.Clear();

		// Get all scatterer types
		var scattererTypes = TypeLibrary.GetTypes()
			.Where( t => t.TargetType != null )
			.Where( t => t.TargetType.IsAssignableTo( typeof(Scatterer) ) )
			.Where( t => !t.IsAbstract )
			.Where( t => t.TargetType != typeof(Scatterer) )
			.OrderBy( t => t.Name )
			.ToArray();

		if ( scattererTypes.Length == 0 )
		{
			_comboBox.AddItem( "No scatterer types found" );
			_comboBox.Enabled = false;
			return;
		}

		// Add each scatterer type to the combobox
		foreach ( var type in scattererTypes )
		{
			var typeName = type.Name;
			var isSelected = currentValue == typeName;

			_comboBox.AddItem( typeName, "category", null, null, isSelected, true );
		}

		// Set current selection
		if ( !string.IsNullOrEmpty( currentValue ) )
		{
			_comboBox.CurrentText = currentValue;
		}
	}
}
