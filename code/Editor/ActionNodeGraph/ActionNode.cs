using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Editor.NodeEditor;
using Facepunch.ActionJigs;
using Sandbox;

namespace Editor.ActionJigs;

public record struct ActionNodeType( NodeDefinition Definition ) : INodeType
{
	public string Identifier => Definition.Identifier;

	public DisplayInfo DisplayInfo => new DisplayInfo
	{
		Name = Definition.DisplayInfo.Title,
		Description = Definition.DisplayInfo.Description,
		Tags = Definition.DisplayInfo.Tags,
		Group = Definition.DisplayInfo.Category
	};

	public bool HasInput( Type valueType )
	{
		return Definition.Bind( null, null ).Inputs.Values
			.Any( x => x.Type.IsAssignableFrom( valueType ) );
	}

	public bool HideInEditor => Identifier == "event";

	public INode CreateNode( IGraph graph )
	{
		var actionGraph = (ActionGraph)graph;
		var node = actionGraph.Jig.AddNode( Definition );
		return new ActionNode( actionGraph, node );
	}
}

public class ActionNode : INode
{
	public INodeType Type => new ActionNodeType( Definition );
	public ActionGraph Graph { get; }
	public Node Node { get; }
	public NodeDefinition Definition => Node.Definition;

	public event Action PlugsChanged;

	public string Identifier { get; }

	DisplayInfo INode.DisplayInfo => new ()
	{
		Name = Definition.DisplayInfo.Title,
		Description = Definition.DisplayInfo.Description,
		Tags = Definition.DisplayInfo.Tags
	};

	public Vector2 Position
	{
		get => _position.Value;
		set => _position.Value = value;
	}

	public Vector2 ExpandSize
	{
		get => _expandSize.Value;
		set => _expandSize.Value = value;
	}

	public class PlugCollection : IEnumerable<IPlug>
	{
		public static PlugCollection Create<T, TDef>( ActionNode node, Func<IReadOnlyDictionary<string, T>> getParams )
			where T : Node.Parameter<TDef>
			where TDef : class, IParameterDefinition
		{
			return new PlugCollection(
				typeof(T) == typeof(Node.Input)
					? () => getParams().Values.Select( x => (Node.Input)(object)x )
						.SelectMany( x =>
							x.LinkArray?
								.Select( ( _, i ) => (x.Name, (int?)i) )
								.Concat( new (string, int?)[] { (x.Name, x.LinkArray.Count) } ) ??
							new (string, int?)[] { (x.Name, null) } )
					: () => getParams().Keys.Select( x => (x, (int?)null) ),
				key => new ActionPlug<T, TDef>( node, getParams()[key.Name], key.Index ) );
		}

		private readonly Func<IEnumerable<(string Name, int? Index)>> _getKeys;
		private readonly Func<(string Name, int? Index), IActionPlug> _createPlug;

		private readonly Dictionary<(string Name, int? Index), IActionPlug> _plugs = new();

		private PlugCollection( Func<IEnumerable<(string Name, int? Index)>> getKeys, Func<(string Name, int? Index), IActionPlug> createPlug )
		{
			_getKeys = getKeys;
			_createPlug = createPlug;
		}

		public bool Update()
		{
			var keys = _getKeys().ToArray();
			var changed = false;

			foreach ( var key in _plugs.Keys.Where( x => !keys.Contains( x ) ).ToArray() )
			{
				Log.Info( $"Removed {key}" );
				_plugs.Remove( key );
				changed = true;
			}

			foreach ( var key in keys )
			{
				if ( _plugs.TryGetValue( key, out var plug ) )
				{
					if ( plug.LastType != plug.Type )
					{
						Log.Info( $"Changed {key}" );
						plug.LastType = plug.Type;
						changed = true;
					}
				}
				else
				{
					Log.Info( $"Added {key}" );
					_plugs[key] = _createPlug( key );
					changed = true;
				}
			}

			return changed;
		}

		public IPlug this[ string name, int? index ] => _plugs[(name, index)];
		public IPlug this[ string name ] => _plugs[(name, null)];

		IEnumerator<IPlug> IEnumerable<IPlug>.GetEnumerator() => _plugs.Values.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => _plugs.Values.GetEnumerator();
	}

	IEnumerable<IPlug> INode.Inputs => Inputs;

	IEnumerable<IPlug> INode.Outputs => Outputs;

	void INode.OnPaint( Rect rect )
	{
		if ( Node.HasErrors() )
		{
			Paint.SetBrush( Color.Red.WithAlpha( 0.25f ) );
			Paint.DrawRect( rect, 5f );
		}
	}

	NodeUI INode.CreateUI( GraphView view )
	{
		return new NodeUI( view, this );
	}

	private readonly UserDataProperty<Vector2> _position;
	private readonly UserDataProperty<Vector2> _expandSize;

	public PlugCollection Inputs { get; }
	public PlugCollection Outputs { get; }

	public ActionNode( ActionGraph graph, Node node )
	{
		Graph = graph;
		Node = node;
		Identifier = $"{node.Id}";

		_position = new UserDataProperty<Vector2>( node.UserData, nameof(Position) );
		_expandSize = new UserDataProperty<Vector2>( node.UserData, nameof(ExpandSize) );

		Inputs = PlugCollection.Create<Node.Input, InputDefinition>( this, () => node.Inputs );
		Outputs = PlugCollection.Create<Node.Output, OutputDefinition>( this, () => node.Outputs );

		Update();
	}

	public void MarkDirty()
	{
		Graph.MarkDirty( this );
	}

	public void Update()
	{
		if ( Inputs.Update() | Outputs.Update() )
		{
			PlugsChanged?.Invoke();
		}
	}
}

public interface IActionPlug : IPlug
{
	Type LastType { get; set; }
}

public class ActionPlug<T, TDef> : IActionPlug
	where T : Node.Parameter<TDef>
	where TDef : class, IParameterDefinition
{
	public ActionNode Node { get; }
	public T Parameter { get; }
	public int? Index { get; set; }

	public Type LastType { get; set; }

	INode IPlug.Node => Node;

	public string Identifier => Parameter.Name;

	public Type Type => Parameter.Type;

	public DisplayInfo DisplayInfo => new()
	{
		Name = Index == null ? Parameter.Display.Title
			: $"{Parameter.Display.Title}[{Index}]",
		Description = Parameter.Display.Description
	};

	public ActionPlug( ActionNode node, T parameter, int? index )
	{
		Node = node;
		Parameter = parameter;
		Index = index;

		LastType = Type;
	}

	public ValueEditor CreateEditor( NodeUI node, Plug plug )
	{
		if ( Parameter is not Node.Input input )
		{
			return null;
		}

		if ( Type == typeof( float ) )
		{
			var slider = new FloatEditor( plug ) { Title = DisplayInfo.Name, Node = node };
			slider.Bind( "Value" ).From( input, "Value" );
			return slider;
		}

		// TODO: int, bool, string, ...

		return null;
	}

	public IPlug ConnectedOutput
	{
		get
		{
			if ( Parameter is not Node.Input input )
			{
				return null;
			}

			if ( Index is null && input.Link is { } link )
			{
				return Node.Graph.FindNode( link.Source.Node )?.Outputs[link.Source.Name];
			}

			if ( Index is not {} index || input.LinkArray is not { } links )
			{
				return null;
			}

			if ( index < 0 || index >= links.Count )
			{
				return null;
			}

			link = links[index];

			return Node.Graph.FindNode( link.Source.Node )?.Outputs[link.Source.Name];
		}
		set
		{
			if ( Parameter is not Node.Input input )
			{
				return;
			}

			if ( value is null )
			{
				if ( Index is null || Index is 0 && input.LinkArray?.Count == 1 )
				{
					input.ClearLinks();
				}
				else
				{
					input.SetLinks( (input.LinkArray ?? Array.Empty<Link>())
						.Select( ( x, i ) => i == Index ? null : x.Source )
						.Where( x => x != null )
						.ToArray() );
				}

				Node.MarkDirty();
				return;
			}

			if ( value is not ActionPlug<Node.Output, OutputDefinition> { Parameter: {} output } )
			{
				return;
			}

			if ( !input.IsArray || value.Type.IsArray && (Index is null || Index is 0 && input.LinkArray?.Count == 1) )
			{
				input.SetLink( output );
				Node.MarkDirty();
				return;
			}

			Index ??= 0;

			var links = (input.LinkArray! ?? Array.Empty<Link>())
				.Select( x => x.Source )
				.ToList();

			if ( Index == links.Count )
			{
				links.Add( output );
			}
			else
			{
				links[Index ?? 0] = output;
			}

			input.SetLinks( links );
			Node.MarkDirty();
		}
	}
}
