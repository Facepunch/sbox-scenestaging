using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Linq;

/// <summary>
/// When applied to a component, the component will be able to provide the color to use for certain UI editor elements
/// </summary>
public interface IComponentColorProvider
{
	Color ComponentColor { get; }
}
