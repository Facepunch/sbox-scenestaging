using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Linq;

/// <summary>
/// When applied to a component, the editor will draw a selectable handle sprite for the gameobject in scene
/// </summary>
public class EditorHandleAttribute : System.Attribute
{
	public string Texture { get; set; }

	public EditorHandleAttribute( string texture )
	{
		Texture = texture;
	}
}
