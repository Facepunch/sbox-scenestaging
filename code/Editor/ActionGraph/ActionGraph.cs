using System;
using Editor.NodeEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Facepunch.ActionGraphs;
using Sandbox;

namespace Editor.ActionGraph;

public partial class ActionGraph : IGraph
{
	[HideInEditor]
	public Facepunch.ActionGraphs.ActionGraph Graph { get; }

	[HideInEditor]
	private Dictionary<Node, ActionNode> NodeDict { get; } = new ();

	[HideInEditor]
	private Dictionary<string, ActionNode> NodeIdDict { get; } = new ();

	[HideInEditor]
	private HashSet<ActionNode> DirtyNodes { get; } = new ();

	[HideInEditor]
	public IEnumerable<INode> Nodes => NodeDict.Values;

	public string Title
	{
		get => Graph.Title;
		set => Graph.Title = value;
	}

	public string Description
	{
		get => Graph.Description;
		set => Graph.Description = value;
	}

	public string Icon
	{
		get => Graph.Icon;
		set => Graph.Icon = value;
	}

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

		if ( !actionNode.Node.IsValid )
		{
			return;
		}

		actionNode.Node.Remove();

		var referencedVars = actionNode.Node.Properties.Values
			.Select( x => x.Value )
			.OfType<Variable>()
			.ToArray();

		foreach ( var variable in referencedVars )
		{
			if ( variable.IsValid && !variable.References.Any() )
			{
				Log.Info( $"No more references to {variable}" );
				Graph.RemoveVariable( variable );
			}
		}
	}

	internal void MarkDirty( ActionNode node )
	{
		DirtyNodes.Add( node );
	}

	public ActionNode FindNode( Node node ) => NodeDict.TryGetValue( node, out var editorNode ) ? editorNode : null;

	public INode FindNode( string identifier ) => NodeIdDict.TryGetValue( identifier, out var node ) ? node : null;

	public string SerializeNodes( IEnumerable<INode> nodes )
	{
		var sourceNodes = nodes.OfType<ActionNode>()
			.Select( x => x.Node )
			.ToArray();

		return Graph.Serialize( sourceNodes, EditorJsonOptions );
	}

	public IEnumerable<INode> DeserializeNodes( string serialized )
	{
		var result = Graph.DeserializeInsert( serialized, EditorJsonOptions );

		UpdateNodes();

		return result.Nodes.Select( x => NodeDict[x] );
	}

	public ActionGraph( Facepunch.ActionGraphs.ActionGraph graph )
	{
		Graph = graph;

		UpdateNodes();
	}

	/// <summary>
	/// Keep <see cref="NodeDict"/> in sync with <see cref="Graph"/>.
	/// </summary>
	private void UpdateNodes()
	{
		foreach ( var node in Graph.Nodes )
		{
			if ( !NodeDict.ContainsKey( node ) )
			{
				var editorNode = ActionNodeType.CreateEditorNode( this, node );
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
		var messages = Graph.Messages;

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
