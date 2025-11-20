using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Unified clutter scattering component supporting both infinite streaming and baked volumes.
/// </summary>
public sealed partial class ClutterComponent : Component, Component.ExecuteInEditor
{
	/// <summary>
	/// The isotope containing objects to scatter and scatter settings.
	/// </summary>
	[Property]
	public ClutterIsotope Isotope { get; set; }

	/// <summary>
	/// Random seed for deterministic generation. Change to get different variations.
	/// </summary>
	[Property]
	public int RandomSeed { get; set; }

	/// <summary>
	/// Enable infinite streaming mode - generates tiles around camera.
	/// Disable for baked volume mode - generates once within bounds.
	/// </summary>
	[Property]
	public bool Infinite
	{
		get => field;
		set
		{
			if ( field != value )
			{
				field = value;
				OnModeChanged();
			}
		}
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( Infinite )
		{
			EnableInfinite();
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( Infinite )
		{
			DisableInfinite();
		}
	}

	protected override void OnUpdate()
	{
		if ( Infinite )
		{
			UpdateInfinite();
		}
	}

	protected override void OnValidate()
	{
		base.OnValidate();

		if ( Infinite && _layer != null )
		{
			RegenerateAllTiles();
		}
	}

	protected override void DrawGizmos()
	{
		if ( !Infinite )
		{
			DrawVolumeGizmos();
		}
	}

	private void OnModeChanged()
	{
		if ( Infinite )
		{
			ClearVolume();
			EnableInfinite();
		}
		else
		{
			DisableInfinite();
		}
	}
}
