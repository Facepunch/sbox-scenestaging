using System;
using Editor.NodeEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Facepunch.ActionJigs;
using Sandbox;

namespace Editor.ActionJigs;

public partial class ActionGraph : IGraph
{
	[HideInEditor]
	public ActionJig Jig { get; }

	private Dictionary<Node, ActionNode> NodeDict { get; } = new Dictionary<Node, ActionNode>();
	private Dictionary<string, ActionNode> NodeIdDict { get; } = new Dictionary<string, ActionNode>();
	private HashSet<ActionNode> DirtyNodes { get; } = new HashSet<ActionNode>();

	[HideInEditor]
	public IEnumerable<INode> Nodes => NodeDict.Values;

	public void AddNode( INode node )
	{
		if ( node is not ActionNode actionNode )
		{
			return;
		}

		NodeDict[actionNode.Node] = actionNode;
		NodeIdDict[actionNode.Identifier] = actionNode;
	}

	public void RemoveNode( INode node )
	{
		if ( node is not ActionNode actionNode )
		{
			return;
		}

		NodeDict.Remove( actionNode.Node );
		NodeIdDict.Remove( node.Identifier );

		actionNode.Node.Remove();
	}

	internal void MarkDirty( ActionNode node )
	{
		DirtyNodes.Add( node );
	}

	public ActionNode FindNode( Node node ) => NodeDict.TryGetValue( node, out var editorNode ) ? editorNode : null;

	public INode FindNode( string identifier ) => NodeIdDict.TryGetValue( identifier, out var node ) ? node : null;

	public string SerializeNodes( IEnumerable<INode> nodes )
	{
		throw new System.NotImplementedException();
	}

	public IEnumerable<INode> DeserializeNodes( string serialized )
	{
		throw new System.NotImplementedException();
	}

	public ActionGraph( ActionJig jig )
	{
		Jig = jig;

		UpdateNodes();
	}

	/// <summary>
	/// Keep <see cref="NodeDict"/> in sync with <see cref="Jig"/>.
	/// </summary>
	private void UpdateNodes()
	{
		foreach ( var node in Jig.Nodes )
		{
			if ( !NodeDict.ContainsKey( node ) )
			{
				var editorNode = new ActionNode( this, node );
				NodeDict.Add( node, editorNode );
				NodeIdDict.Add( editorNode.Identifier, editorNode );
			}
		}

		foreach ( var (node, editorNode) in NodeDict.Where( x => !x.Key.IsValid ).ToArray() )
		{
			NodeDict.Remove( node );
			NodeIdDict.Remove( editorNode.Identifier );
		}
	}

	public IReadOnlyList<INode> Update()
	{
		var messages = Jig.Messages;

		if ( DirtyNodes.Count == 0 )
		{
			return Array.Empty<INode>();
		}

		var dirty = DirtyNodes.ToArray();

		foreach ( var actionNode in dirty )
		{
			actionNode.Update();
		}

		DirtyNodes.Clear();

		return dirty;
	}
}
