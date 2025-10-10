using System;
using System.Linq;

namespace Editor;

/// <summary>
/// Dropdown widget to display all Scatterer using the type library
/// </summary>
[CustomEditor( typeof( string ), NamedEditor = "scatterer_type" )]
public class ScattererTypeControlWidget : DropdownControlWidget
{
	public ScattererTypeControlWidget( SerializedProperty property ) : base( property )
	{
		IsMultiSelect = false;
	}

	protected override void PaintDisplayText( Rect rect, Color color )
	{
		var currentValue = SerializedProperty.GetValue<string>();

		string displayText = "None";
		if ( !string.IsNullOrEmpty( currentValue ) )
		{
			var scattererType = Game.TypeLibrary.GetTypes<IProceduralScatterer>()
				.FirstOrDefault( t => !t.IsAbstract && !t.IsInterface && t.Name == currentValue );

			if ( scattererType != null )
			{
				var instance = scattererType.Create<IProceduralScatterer>();
				displayText = instance?.ToString() ?? currentValue;
			}
			else
			{
				displayText = currentValue;
			}
		}

		Paint.SetPen( color );
		Paint.DrawText( rect, displayText, TextFlag.LeftCenter );
	}

	protected override void PopulateMenu( Widget canvas )
	{
		var currentValue = SerializedProperty.GetValue<string>();

		// Get all IScatterer types from TypeLibrary
		var scattererTypes = Game.TypeLibrary.GetTypes<IProceduralScatterer>()
			.Where( t => !t.IsAbstract && !t.IsInterface )
			.OrderBy( t => t.Name )
			.ToList();

		// If current value is empty and we have types, default to first one
		if ( string.IsNullOrEmpty( currentValue ) && scattererTypes.Count != 0 )
		{
			currentValue = scattererTypes.First().Name;
			SerializedProperty.SetValue( currentValue );
		}

		foreach ( var type in scattererTypes )
		{
			var typeName = type.Name;
			var instance = type.Create<IProceduralScatterer>();
			var displayName = instance?.ToString() ?? typeName;

			AddMenuOption( canvas, displayName, "forest",
				isSelected: () => string.Equals( SerializedProperty.GetValue<string>(), typeName, StringComparison.OrdinalIgnoreCase ),
				onSelect: () => SerializedProperty.SetValue( typeName )
			);
		}
	}
}
