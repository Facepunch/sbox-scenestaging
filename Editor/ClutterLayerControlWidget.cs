using System;
using static Sandbox.ClutterInstance;

namespace Editor;

[CustomEditor( typeof( ClutterLayer ), NamedEditor = "clutter_layer" )]
public class ClutterLayerControlWidget : ControlWidget
{
	public ClutterLayerControlWidget( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Column();
		Layout.Spacing = 2;
		AcceptDrops = false;

		Rebuild();
	}

	void Rebuild()
	{
		Layout.Clear( true );

		var comboBox = new ComboBox( this );
		var currentValue = SerializedProperty.GetValue<ClutterLayer>();

		// Add empty option for "None"
		comboBox.AddItem( "None",
			onSelected: () => SerializedProperty.SetValue( (ClutterLayer)null ),
			selected: currentValue == null );

		// Get all layers from the ClutterSystem
		var activeScene = SceneEditorSession.Active?.Scene;
		if ( activeScene != null )
		{
			var clutterSystem = activeScene.GetSystem<ClutterSystem>();
			if ( clutterSystem != null )
			{
				foreach ( var layer in clutterSystem.GetAllLayers() )
				{
					var layerName = layer.Name;
					var objectCount = layer.Objects?.Count ?? 0;
					var displayName = $"{layerName} ({objectCount} objects)";

					comboBox.AddItem( displayName,
						onSelected: () => SerializedProperty.SetValue( layer ),
						selected: currentValue == layer );
				}
			}
		}

		Layout.Add( comboBox );
	}
}
