using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Editor.NodeEditor;
using Facepunch.ActionJigs;
using Sandbox;
using Sandbox.UI;

namespace Editor.ActionJigs;

public partial class MainWindow : DockWindow
{
	private static Dictionary<ActionJig, MainWindow> Instances { get; } = new Dictionary<ActionJig, MainWindow>();

	public static MainWindow Open( ActionJig actionJig, string name )
	{
		if ( !Instances.TryGetValue( actionJig, out var inst ) )
		{
			Instances[actionJig] = inst = new MainWindow( actionJig, name );
		}

		inst.Show();
		inst.Focus();
		return inst;
	}

	public ActionJig ActionJig { get; }

	public ActionGraph Graph { get; }
	public GraphView View { get; }

	public event Action Saved;

	private MainWindow( ActionJig actionJig, string name )
	{
		DeleteOnClose = true;

		ActionJig = actionJig;
		Graph = new ActionGraph( actionJig );
		View = new GraphView( null )
		{
			Graph = Graph,
			WindowTitle = "Graph View"
		};

		foreach ( var nodeDefinition in EditorNodeLibrary.All.Values )
		{
			View.AddNodeType( new ActionNodeType( nodeDefinition ) );
		}

		Title = $"{name} - Action Graph";
		Size = new Vector2( 1280, 720 );

		RebuildUI();
	}

	[Event.Hotload]
	public void RebuildUI()
	{
		DockManager.Clear();
		DockManager.AddDock( null, View );

		MenuBar.Clear();

		{
			var file = MenuBar.AddMenu( "File" );
			file.AddOption( new Option( "Save" ) { Shortcut = "Ctrl+S", Triggered = Save } );
			file.AddSeparator();
			file.AddOption( new Option( "Exit" ) { Triggered = Close } );
		}
	}

	private void Save()
	{
		Saved?.Invoke();
	}

	protected override bool OnClose()
	{
		if ( Instances.TryGetValue( ActionJig, out var inst ) && inst == this )
		{
			Instances.Remove( ActionJig );
		}

		return base.OnClose();
	}
}
