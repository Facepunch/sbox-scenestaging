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
				() => getParams().Keys,
				name => new ActionPlug<T, TDef>( node, getParams()[name] ) );
		}

		private readonly Func<IEnumerable<string>> _getKeys;
		private readonly Func<string, IPlug> _createPlug;

		private readonly Dictionary<string, IPlug> _plugs = new();

		private PlugCollection( Func<IEnumerable<string>> getKeys, Func<string, IPlug> createPlug )
		{
			_getKeys = getKeys;
			_createPlug = createPlug;
		}

		public void Update()
		{
			var keys = _getKeys().ToHashSet();

			foreach ( var key in _plugs.Keys.Where( x => !keys.Contains( x ) ).ToArray() )
			{
				_plugs.Remove( key );
			}

			foreach ( var key in keys )
			{
				if ( !_plugs.ContainsKey( key ) )
				{
					_plugs[key] = _createPlug( key );
				}
			}
		}

		public IPlug this[ string name ] => _plugs[name];

		IEnumerator<IPlug> IEnumerable<IPlug>.GetEnumerator() => _plugs.Values.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => _plugs.Values.GetEnumerator();
	}

	IEnumerable<IPlug> INode.Inputs => Inputs;

	IEnumerable<IPlug> INode.Outputs => Outputs;

	void INode.OnPaint( Rect rect )
	{

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

	public void Update()
	{
		Inputs.Update();
		Outputs.Update();
	}
}

public class ActionPlug<T, TDef> : IPlug
	where T : Node.Parameter<TDef>
	where TDef : class, IParameterDefinition
{
	public ActionNode Node { get; }
	public T Parameter { get; }

	INode IPlug.Node => Node;

	public string Identifier => Parameter.Name;

	public Type Type => Parameter.Type;

	public DisplayInfo DisplayInfo => new()
	{
		Name = Parameter.Display.Title,
		Description = Parameter.Display.Description
	};

	public ActionPlug( ActionNode node, T parameter )
	{
		Node = node;
		Parameter = parameter;
	}

	public ValueEditor CreateEditor( NodeUI node, Plug plug )
	{
		// TODO
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

			if ( input.Link is not { } link )
			{
				return null;
			}

			return Node.Graph.FindNode( link.Source.Node )?.Outputs[link.Source.Name];
		}
		set
		{
			if ( value is not ActionPlug<Node.Output, OutputDefinition> { Parameter: {} output } )
			{
				return;
			}

			if ( Parameter is not Node.Input input )
			{
				return;
			}

			input.SetLink( output );
		}
	}
}
