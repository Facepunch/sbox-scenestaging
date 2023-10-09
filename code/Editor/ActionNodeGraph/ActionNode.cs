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
	private static HashSet<string> Hidden { get; } = new ()
	{
		"event", "property.get", "property.set"
	};

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
		var isSignal = valueType == typeof(OutputSignal);
		return Definition.Bind( null, null ).Inputs.Values
			.Any( x => isSignal ? x.IsSignal : x.Type.IsAssignableFrom( valueType ) );
	}

	public bool HideInEditor => Hidden.Contains( Identifier );

	public INode CreateNode( IGraph graph )
	{
		var actionGraph = (ActionGraph)graph;
		var node = actionGraph.Jig.AddNode( Definition );
		return new ActionNode( actionGraph, node );
	}
}

public enum PropertyNodeKind
{
	Get,
	Set
}

public record struct PropertyNodeType( PropertyDescription Property, PropertyNodeKind Kind, bool ReadWrite ) : INodeType
{
	public DisplayInfo DisplayInfo => new ()
	{
		Name = ReadWrite ? $"{Property.Title}/{Kind}" : $"{Property.Title} ({Kind})",
		Description = Property.Description,
		Group = $"*{Property.TypeDescription.Name}"
	};

	public bool HasInput( Type valueType )
	{
		return Kind == PropertyNodeKind.Set && valueType == typeof(OutputSignal)
			|| Property.TypeDescription.TargetType.IsAssignableFrom( valueType )
			|| Property.PropertyType.IsAssignableFrom( valueType );
	}

	public bool HideInEditor => false;

	public INode CreateNode( IGraph graph )
	{
		var actionGraph = (ActionGraph)graph;
		var node = actionGraph.Jig.AddNode( Kind switch
		{
			PropertyNodeKind.Set => actionGraph.Jig.NodeLibrary.SetProperty,
			PropertyNodeKind.Get => actionGraph.Jig.NodeLibrary.GetProperty,
			_ => throw new NotImplementedException()
		} );

		node.Properties["type"].Value = Property.TypeDescription.TargetType;
		node.Properties["property"].Value = Property.Name;

		return new ActionNode( actionGraph, node );
	}

	public string Identifier => $"property.{Kind}/{Property.TypeDescription.FullName}/{Property.Name}";
}

public class ActionNode : INode
{
	public INodeType Type => new ActionNodeType( Definition );
	public ActionGraph Graph { get; }
	public Node Node { get; }
	public NodeDefinition Definition => Node.Definition;

	public event Action PlugsChanged;

	public string Identifier { get; }

	public string ErrorMessage => string.Join( Environment.NewLine,
		Node.GetMessages()
			.Where( x => x.IsError )
			.Select( FormatMessage ) );

	private static string FormatProperty( Node.Property property )
	{
		return property.Definition.Display.Title ?? property.Name;
	}

	private static string FormatInput( Node.Input input )
	{
		return input.Definition.Display.Title ?? input.Name;
	}

	private static string FormatOutput( Node.Output output )
	{
		return output.Definition.Display.Title ?? output.Name;
	}

	private string FormatMessage( ValidationMessage message )
	{
		return message.Context switch
		{
			Link link when link.Target.Node == Node => $"{FormatInput(link.Target)}: {message.Value}",
			Node.Property property when property.Node == Node => $"{FormatProperty( property )}: {message.Value}",
			Node.Input input when input.Node == Node => $"{FormatInput(input)}: {message.Value}",
			Node.Output output when output.Node == Node => $"{FormatOutput( output )}: {message.Value}",
			_ => message.Value
		};
	}

	private DisplayInfo PropertyDisplayInfo( PropertyNodeKind kind )
	{
		var name = Node.Properties["property"].Value as string;

		return new DisplayInfo { Name = $"{kind} {name}" };
	}

	DisplayInfo INode.DisplayInfo
	{
		get
		{
			switch ( Definition.Identifier )
			{
				case "property.get":
					return PropertyDisplayInfo( PropertyNodeKind.Get );

				case "property.set":
					return PropertyDisplayInfo( PropertyNodeKind.Set );

				default:
					return
						new()
						{
							Name = Definition.DisplayInfo.Title,
							Description = Definition.DisplayInfo.Description,
							Tags = Definition.DisplayInfo.Tags
						};
			}
		}
	}

	public Color PrimaryColor
	{
		get
		{
			if ( Node.HasErrors() )
			{
				return Theme.Red;
			}

			return Node.Definition.Kind switch
			{
				NodeKind.Action => Color.Lerp( new Color( 0.7f, 0.7f, 0.7f ), Theme.Blue, 0.5f ),
				NodeKind.Expression => Color.Lerp( new Color( 0.7f, 0.7f, 0.7f ), Theme.Yellow, 0.5f ),
				_ => throw new NotImplementedException()
			};
		}
	}

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
				_plugs.Remove( key );
				changed = true;
			}

			foreach ( var key in keys )
			{
				if ( _plugs.TryGetValue( key, out var plug ) )
				{
					if ( plug.LastType != plug.Type )
					{
						plug.LastType = plug.Type;
						changed = true;
					}
				}
				else
				{
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
		Name = Index == null ? Parameter.Display.Title ?? Parameter.Name
			: $"{Parameter.Display.Title ?? Parameter.Name}[{Index}]",
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
		return null;
	}

	public string ErrorMessage => string.Join( Environment.NewLine, Parameter.GetMessages()
		.Where( x => x.IsError )
		.Select( x => x.Value ) );

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

	public override string ToString()
	{
		return $"{Node.Identifier}.{Identifier}";
	}
}
