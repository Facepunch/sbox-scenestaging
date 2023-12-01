using Editor.EntityPrefabEditor;
using Sandbox;
using Sandbox.TerrainEngine;
using Sandbox.UI;
using System;
using System.IO;

namespace Editor.TerrainEngine;

[CustomEditor( typeof( Terrain ) )]
public partial class TerrainComponentWidget : CustomComponentWidget
{
	SerializedObject SerializedTerrainData { get; set; }

	public TerrainComponentWidget( SerializedObject obj ) : base( obj )
	{
		SetSizeMode( SizeMode.Default, SizeMode.Default );

		Layout = Layout.Column();

		// Can't do shit without this linked
		// We could prompt the user to select one (an asset?)
		var dataProperty = SerializedObject.GetProperty( "TerrainData" );
		if ( dataProperty == null )
		{
			var warningLayout = Layout.Column();
			warningLayout.Margin = new Margin( 8, 8, 8, 0 );
			warningLayout.Add( new WarningBox( "No TerrainData!" ) );
			Layout.Add( warningLayout );
			return;
		}

		if ( !dataProperty.TryGetAsObject( out var data ) )
		{
			return; // ?
		}

		SerializedTerrainData = data;

		var tabs = Layout.Add( new TabWidget( this ) );
		tabs.AddPage( "Sculpt", "construction", new SculptPageWidget( this ) );
		tabs.AddPage( "Paint", "brush", PaintPage() );
		tabs.AddPage( "Settings", "settings", SettingsPage() );
	}
}
