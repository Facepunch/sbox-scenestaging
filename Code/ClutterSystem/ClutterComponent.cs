using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Unified clutter scattering component supporting both infinite streaming and baked volumes.
/// </summary>
public sealed partial class ClutterComponent : Component, Component.ExecuteInEditor
{
	/// <summary>
	/// The clutter containing objects to scatter and scatter settings.
	/// </summary>
	[Property]
	public ClutterDefinition Clutter { get; set; }

	/// <summary>
	/// Random seed for deterministic generation. Change to get different variations.
	/// </summary>
	[Property]
	public int RandomSeed { get; set; }

	private bool _infinite;

	/// <summary>
	/// Enable infinite streaming mode - generates tiles around camera.
	/// Disable for baked volume mode - generates once within bounds.
	/// </summary>
	[Property]
	public bool Infinite
	{
		get => _infinite;
		set
		{
			if ( _infinite == value )
				return;

			// Clear all clutter before switching modes
			Clear();

			_infinite = value;
		}
	}

	/// <summary>
	/// Clears all infinite mode tiles for this component.
	/// </summary>
	public void ClearInfinite()
	{
		var gridSystem = Scene?.GetSystem<ClutterGridSystem>();
		gridSystem?.ClearComponent( this );
	}

	protected override void OnDisabled()
	{
		Clear();
		base.OnDisabled();
	}

	protected override void DrawGizmos()
	{
		if ( !Infinite )
		{
			DrawVolumeGizmos();
		}
	}
}
