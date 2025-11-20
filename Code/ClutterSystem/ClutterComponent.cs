using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Unified clutter scattering component supporting both infinite streaming and baked volumes.
/// </summary>
public sealed partial class ClutterComponent : Component, Component.ExecuteInEditor
{
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

	/// <summary>
	/// The isotope containing objects to scatter and scatter settings.
	/// </summary>
	[Property]
	public ClutterIsotope Isotope { get; set; }

	/// <summary>
	/// Random seed for deterministic generation. Change to get different variations.
	/// </summary>
	[Property]
	public int RandomSeed
	{
		get => field;
		set
		{
			if ( field != value )
			{
				field = value;
				OnSeedChanged();
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
		
		// Called when properties change in the inspector
		if ( Infinite && _infiniteData != null )
		{
			// Check if settings changed and update immediately
			if ( _infiniteData.TileSize != TileSize || _infiniteData.TileRadius != TileRadius )
			{
				OnInfiniteSettingsChanged();
			}
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
			// Switching TO Infinite mode - clean up volume and enable infinite
			ClearVolume();
			EnableInfinite();
		}
		else
		{
			// Switching TO Volume mode - disable infinite
			DisableInfinite();
		}
	}
}
