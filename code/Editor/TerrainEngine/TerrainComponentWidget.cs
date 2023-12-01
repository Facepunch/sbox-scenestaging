using Editor.EntityPrefabEditor;
using Sandbox;
using Sandbox.TerrainEngine;
using Sandbox.UI;
using System;
using System.IO;
using System.Reflection;

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

		var sculptPage = new SculptPageWidget( this );
		var paintPage = new PaintPageWidget( this );

		tabs.AddPage( "Sculpt", "construction", sculptPage );
		tabs.AddPage( "Paint", "brush", paintPage );
		tabs.AddPage( "Settings", "settings", SettingsPage() );

		// cant be fucked
		var tabbar = typeof( TabWidget ).GetField( "TabBar", BindingFlags.NonPublic | BindingFlags.Instance ).GetValue( tabs ) as SegmentedControl;
		tabbar.OnSelectedChanged += ( value ) =>
		{
			TerrainEditor.Mode = value;
		};

		// set the active tab for the current editor mode
		var pages = typeof( TabWidget ).GetField( "pages", BindingFlags.NonPublic | BindingFlags.Instance ).GetValue( tabs ) as Dictionary<string, Widget>;
		if ( pages.TryGetValue( TerrainEditor.Mode , out var page ) )
		{
			tabs.SetPage( page );
		}
	}
}
