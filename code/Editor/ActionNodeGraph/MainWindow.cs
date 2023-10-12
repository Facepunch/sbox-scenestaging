using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Editor.NodeEditor;
using Facepunch.ActionJigs;

namespace Editor.ActionJigs;

public static class ActionJigExtensions
{
	public static Guid GetGuid( this IActionJig actionJig )
	{
		if ( actionJig.UserData.TryGetPropertyValue( "id", out var node ) && Guid.TryParse( node?.GetValue<string>(), out var guid ) )
		{
			return guid;
		}

		guid = Guid.NewGuid();
		actionJig.UserData["id"] = guid.ToString();

		return guid;
	}

	public static string GetName( this IActionJig actionJig )
	{
		return actionJig.UserData.TryGetPropertyValue( "name", out var node ) ? node?.GetValue<string>() : null;
	}

	private static DisplayInfo PropertyDisplayInfo( Node node, PropertyNodeKind kind )
	{
		var name = node.Properties["property"].Value as string;

		return new DisplayInfo { Name = $"{kind} {name}" };
	}

	private static DisplayInfo ConstDisplayInfo( Node node )
	{
		var name = node.Properties["name"].Value as string;

		return new DisplayInfo
		{
			Name = string.IsNullOrEmpty( name ) ? node.Definition.DisplayInfo.Title : name,
			Description = node.Definition.DisplayInfo.Description,
			Tags = node.Definition.DisplayInfo.Tags
		};
	}

	public static DisplayInfo GetDisplayInfo( this Node node )
	{
		switch ( node.Definition.Identifier )
		{
			case "property.get":
				return PropertyDisplayInfo( node, PropertyNodeKind.Get );

			case "property.set":
				return PropertyDisplayInfo( node, PropertyNodeKind.Set );

			case { } s when s.StartsWith( "const." ):
				return ConstDisplayInfo( node );

			default:
				return
					new()
					{
						Name = node.Definition.DisplayInfo.Title,
						Description = node.Definition.DisplayInfo.Description,
						Tags = node.Definition.DisplayInfo.Tags
					};
		}
	}
}

public partial class MainWindow : DockWindow
{
	private static Dictionary<Guid, MainWindow> Instances { get; } = new Dictionary<Guid, MainWindow>();

	[StackTraceBuilder]
	public static void BuildStackTrace( Widget parent, NodeInvocationException e )
	{
		var row = parent.Layout.AddRow();
		row.Spacing = 8;
		row.Margin = 8;

		var stack = new List<NodeInvocationException>();
		var baseException = (Exception) e;

		while ( true )
		{
			stack.Add( e );

			if ( e.InnerException is NodeInvocationException inner )
			{
				e = inner;
			}
			else
			{
				baseException = e.InnerException ?? e;
				break;
			}
		}

		stack.Reverse();

		var message = row.Add( new Label( baseException.Message ), 1 );
		message.WordWrap = true;

		var button = new Button( "Copy To Clipboard" );
		button.Clicked = () =>
		{
			var message = baseException.Message;
			message += "\n";
			message += string.Join( "\n", stack.Select( x => x.Node.GetDisplayInfo().Name ) );
			EditorUtility.Clipboard.Copy( message );
		};

		row.Add( button );

		foreach ( var frame in stack )
		{
			AddStackLine( frame.Node, parent.Layout );
		}
	}

	private static void AddStackLine( Node node, Layout target )
	{
		if ( node == null )
			return;

		var row = new StackRow( node.GetDisplayInfo().Name, node.ActionJig.GetName() );
		row.IsFromEngine = false;
		row.MouseClick += () =>
		{
			var window = Open( node.ActionJig );
			var matchingNode = node.ActionJig == window.ActionJig
				? node
				: window.ActionJig.Nodes.FirstOrDefault( x => x.Id == node.Id );

			window.View.SelectNode( matchingNode );
			window.View.CenterOnSelection();
		};

		target.Add( row );
	}

	public static MainWindow Open( ActionJig actionJig, string name = null )
	{
		if ( actionJig.GetName() == null )
		{
			actionJig.UserData["name"] = name;
		}

		var guid = actionJig.GetGuid();

		if ( !Instances.TryGetValue( guid, out var inst ) )
		{
			Instances[guid] = inst = new MainWindow( actionJig );
		}

		inst.Show();
		inst.Focus();
		return inst;
	}

	public ActionJig ActionJig { get; }

	public ActionGraph Graph { get; }
	public ActionGraphView View { get; private set; }
	public Properties Properties { get; private set; }
	public ErrorList ErrorList { get; private set; }

	public event Action Saved;

	private MainWindow( ActionJig actionJig )
	{
		DeleteOnClose = true;

		ActionJig = actionJig;
		Graph = new ActionGraph( actionJig );

		Title = $"{ActionJig.GetName()} - Action Graph";
		Size = new Vector2( 1280, 720 );

		RebuildUI();
	}

	[EditorEvent.Frame]
	public void Frame()
	{
		var updated = Graph.Update();

		if ( updated.Any() )
		{
			View.UpdateConnections( updated );
		}
	}

	[Event.Hotload]
	public void RebuildUI()
	{
		View = new ActionGraphView( null )
		{
			Graph = Graph,
			WindowTitle = "Graph View"
		};
		View.SetBackgroundImage( "toolimages:/grapheditor/grapheditorbackgroundpattern_shader.png" );
		View.OnSelectionChanged += View_OnSelectionChanged;

		foreach ( var nodeDefinition in EditorNodeLibrary.All.Values )
		{
			View.AddNodeType( new ActionNodeType( nodeDefinition ) );
		}

		Properties = new Properties( null )
		{
			Target = Graph
		};

		ErrorList = new ErrorList( null, this );

		DockManager.Clear();
		DockManager.RegisterDockType( "Properties", "edit", () => new Properties(null) { Target = Graph }, false );
		DockManager.RegisterDockType( "ErrorList", "error", () => new ErrorList( null, this ), false );

		DockManager.AddDock( null, View, DockArea.Right, properties: DockManager.DockProperty.HideCloseButton | DockManager.DockProperty.HideOnClose );
		DockManager.AddDock( null, Properties, DockArea.Left, DockManager.DockProperty.HideOnClose, split: 0.33f );
		DockManager.AddDock( Properties, ErrorList, DockArea.Bottom, DockManager.DockProperty.HideOnClose, split: 0.75f );
		DockManager.Update();

		MenuBar.Clear();

		{
			var file = MenuBar.AddMenu( "File" );
			file.AddOption( new Option( "Save" ) { Shortcut = "Ctrl+S", Triggered = Save } );
			file.AddSeparator();
			file.AddOption( new Option( "Exit" ) { Triggered = Close } );
		}
	}

	private void View_OnSelectionChanged()
	{
		var items = View.SelectedItems.ToArray();

		if ( items is [NodeUI node] )
		{
			Properties.Target = node.Node;
		}
		else
		{
			Properties.Target = Graph;
		}
	}

	private void Save()
	{
		Saved?.Invoke();
	}

	protected override bool OnClose()
	{
		var guid = ActionJig.GetGuid();

		if ( Instances.TryGetValue( guid, out var inst ) && inst == this )
		{
			Instances.Remove( guid );
		}

		return base.OnClose();
	}
}

public class ActionGraphView : GraphView
{
	public new ActionGraph Graph
	{
		get => (ActionGraph)base.Graph;
		set => base.Graph = value;
	}

	public ActionGraphView( Widget parent ) : base( parent )
	{

	}

	public void SelectNode( Node node )
	{
		var actionNode = Graph.FindNode( node );

		SelectNode( actionNode );
	}

	public void SelectLink( Link link )
	{
		SelectLinks( new [] { link } );
	}

	public void SelectLinks( IEnumerable<Link> links )
	{
		var linkSet = links.Select( x => (x.Source, x.Target) ).ToHashSet();

		var connections = Items.OfType<Connection>().Where( x =>
			x.Input.Inner is ActionPlug<Node.Input, InputDefinition> { Parameter: { } input } &&
			x.Output.Inner is ActionPlug<Node.Output, OutputDefinition> { Parameter: { } output } &&
			linkSet.Contains( (output, input) ) );

		foreach ( var item in SelectedItems )
		{
			item.Selected = false;
		}

		foreach ( var connection in connections )
		{
			connection.Selected = true;
		}
	}

	private static IEnumerable<INodeType> GetInstanceNodes( TypeDescription typeDesc )
	{
		foreach ( var propertyDesc in typeDesc.Properties )
		{
			if ( propertyDesc.CanRead )
			{
				yield return new PropertyNodeType( propertyDesc, PropertyNodeKind.Get, propertyDesc.CanWrite );
			}

			if ( propertyDesc.CanWrite )
			{
				yield return new PropertyNodeType( propertyDesc, PropertyNodeKind.Set, propertyDesc.CanRead );
			}
		}
	}

	protected override IEnumerable<INodeType> GetRelevantNodes( Type inputValueType )
	{
		var baseNodes = base.GetRelevantNodes( inputValueType );
		var typeDesc = EditorTypeLibrary.GetType( inputValueType );

		if ( typeDesc == null )
		{
			return baseNodes;
		}

		return GetInstanceNodes( typeDesc ).Concat( baseNodes );
	}

	private static Dictionary<Type, HandleConfig> HandleConfigs { get; } = new()
	{
		{ typeof(OutputSignal), new HandleConfig( "Signal", Color.White, HandleShape.Arrow ) },
		{ typeof(Task), new HandleConfig( "Signal", Color.White, HandleShape.Arrow ) },
		{ typeof(GameObject), new HandleConfig( null, Theme.Blue ) },
		{ typeof(BaseComponent), new HandleConfig( null, Theme.Green ) },
		{ typeof(float), new HandleConfig( "Float", Color.Parse( "#8ec07c" )!.Value ) },
		{ typeof(int), new HandleConfig( "Int", Color.Parse( "#ce67e0" )!.Value ) },
		{ typeof(bool), new HandleConfig( "Bool", Color.Parse( "#e0d867" )!.Value ) },
		{ typeof(Vector3), new HandleConfig( "Vector3", Color.Parse( "#7177e1" )!.Value ) },
		{ typeof(string), new HandleConfig( "String", Color.Parse( "#c7ae32" )!.Value ) }
	};

	protected override HandleConfig OnGetHandleConfig( Type type )
	{
		if ( HandleConfigs.TryGetValue( type, out var config ) )
		{
			return config;
		}

		if ( type.BaseType != null )
		{
			return OnGetHandleConfig( type.BaseType );
		}

		return base.OnGetHandleConfig( type );
	}
}
