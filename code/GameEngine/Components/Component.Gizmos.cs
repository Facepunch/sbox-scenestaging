using Sandbox;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public abstract partial class Component
{
	/// <summary>
	/// Called in the editor to draw things like bounding boxes etc
	/// </summary>
	protected virtual void DrawGizmos() { }

	internal void DrawGizmosInternal()
	{
		ExceptionWrap( "DrawGizmos", DrawGizmos );
	}
}
