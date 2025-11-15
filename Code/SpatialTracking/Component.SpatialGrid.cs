using Sandbox;

namespace Sandbox;

/// <summary>
/// Extends Component with SpatialGrid marker interface.
/// Components that implement Component.SpatialGrid are automatically tracked in the spatial grid.
/// </summary>
public partial class Component
{
	/// <summary>
	/// Marker interface for components that should be automatically tracked in the spatial grid.
	/// Usage: public class MyComponent : Component, Component.SpatialGrid
	/// </summary>
	public interface SpatialGrid { }
}
