using Editor;
using System;
using System.IO;

namespace Bugge.UnityImporter;

public class ImportWindow : Window
{
	/// <summary>
	/// Bool: Convert Materials<br/>
	/// Bool: Create Textures
	/// Bool: Split Meshes<br/>
	/// </summary>
	public event Action<bool, bool, bool> OnConfirm;
	public event Action OnCancel;

	public ImportWindow( UnityPackageExtractor.UnityFile[] files )
	{
		WindowTitle = "Import Unity Package";
		SetWindowIcon( "unarchive" );
		Size = new Vector2( 400, 800 );
		StatusBar.Visible = false;

		var canvas = new Widget( null );

		var layout = canvas.Layout = Layout.Column();
		layout.Margin = 10;
		layout.Spacing = 10;

		var scrollArea = layout.Add( new ScrollArea( null ), 1 );
		scrollArea.HorizontalScrollbarMode = ScrollbarMode.Off;

		var scroll = new Widget( null );
		var scrollLayout = scroll.Layout = Layout.Column();
		scrollArea.Canvas = scroll;

		var checkboxes = new Checkbox[files.Length];
		int baseDepth = GetPathDepth( files[0].Path );

		for ( int i = 0; i < files.Length; i++ )
		{
			var file = files[i];

			int depth = GetPathDepth( file.Path ) - baseDepth;

			var row = scrollLayout.Add( new Widget( null ) );
			var rowLayout = row.Layout = Layout.Row();
			rowLayout.Margin = new Sandbox.UI.Margin( depth * 20, 0, 0, 0 );

			string name = file.Path.Split( "/" )[^1];
			var checkbox = rowLayout.Add( new Checkbox( name ) { Value = file.Included } );

			checkbox.Toggled = () =>
			{
				bool isDirectory = !Path.HasExtension( file.Path );
				bool newValue = checkbox.Value;

				file.Included = newValue;
				if ( !isDirectory ) return;

				for ( int j = 0; j < checkboxes.Length; j++ )
				{
					var jFile = files[j];
					string parentDir = file.Path.TrimEnd( '/', '\\' );
					string childPath = jFile.Path.TrimEnd( '/', '\\' );

					bool isInDirectory = childPath.Contains( parentDir ) && jFile.Path != file.Path;
					if ( !isInDirectory ) continue;

					jFile.Included = newValue;
				}
			};

			checkboxes[i] = checkbox;
		}

		scrollLayout.AddStretchCell();

		var toggleAllRow = layout.AddRow();
		toggleAllRow.Spacing = 10;

		var enableAllBtn = toggleAllRow.Add( new Button( "Enable All" ) );
		var disableAllBtn = toggleAllRow.Add( new Button( "Disable All" ) );

		enableAllBtn.Clicked = () =>
		{
			for ( int i = 0; i < checkboxes.Length; i++ )
			{
				var file = files[i];
				var checkbox = checkboxes[i];

				file.Included = true;
				checkbox.Value = true;
				checkbox.Enabled = true;
			}
		};

		disableAllBtn.Clicked = () =>
		{
			for ( int i = 0; i < checkboxes.Length; i++ )
			{
				var file = files[i];
				var checkbox = checkboxes[i];

				file.Included = false;
				checkbox.Value = false;
				if ( i == 0 ) continue;
				checkbox.Enabled = false;
			}
		};

		layout.AddSeparator( true );

		// Settings
		var convertRow = layout.AddRow();
		convertRow.Spacing = 10;

		// var prefabToggle = convertRow.Add( new Checkbox( "Convert Prefabs" ) );
		var materialsToggle = convertRow.Add( new Checkbox( "Convert Materials" ) );
		// var textureToggle = convertRow.Add( new Checkbox( "Convert Textures" ) );

		Checkbox splitMeshesToggle = null;
		if ( MeshSplitterIntegrator.IsAvailable() )
			splitMeshesToggle = convertRow.Add( new Checkbox( "Split Meshes" ) );

		// MeshSplitterIntegrator.CallMeshSplitter();

		// Buttons
		var btnRow = layout.AddRow();
		btnRow.Spacing = 10;

		var confirmBtn = btnRow.Add( new Button( "Confirm", "check" ) );
		var cancelBtn = btnRow.Add( new Button( "Cancel", "cancel" ) );

		confirmBtn.Clicked = () =>
		{
			bool convertMaterials = materialsToggle.Value;
			bool splitMeshes = splitMeshesToggle?.Value ?? false;

			OnConfirm?.Invoke( convertMaterials, false, splitMeshes );
			Close();
		};

		cancelBtn.Clicked = () =>
		{
			OnCancel?.Invoke();
			Close();
		};

		layout.AddStretchCell();

		Canvas = canvas;
	}

	protected override void OnClosed()
	{
		base.OnClosed();
		OnCancel?.Invoke();
	}

	public static int GetPathDepth( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) ) return 0;
		var parts = path.Split( ['/', '\\'], StringSplitOptions.RemoveEmptyEntries );

		return parts.Length;
	}
}
