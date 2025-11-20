namespace Sandbox;

/// <summary>
/// Infinite/streaming clutter mode - generates tiles around camera
/// </summary>
public sealed partial class ClutterComponent
{
	[Property, Group( "Infinite" ), ShowIf( nameof(Infinite), true )]
	public float TileSize { get; set; } = 512f;

	[Property, Group( "Infinite" ), ShowIf( nameof(Infinite), true )]
	public int TileRadius { get; set; } = 4;

	[Property, Group( "Infinite" ), ShowIf( nameof(Infinite), true )]
	public bool UseThisAsCenter { get; set; }

	[Property, Group( "Infinite" ), ShowIf( nameof(Infinite), true ), Range( 1, 50 )]
	public int TilesPerFrame { get; set; } = 5;

	private ClutterGridSystem.ClutterData _infiniteData;
	private ClutterGridSystem _gridSystem;
	private int _lastInfiniteHash;
	private Queue<Vector2Int> _tilesToRegenerate = new();
	private bool _isRegenerating;

	private void EnableInfinite()
	{
		_gridSystem = Scene.GetSystem<ClutterGridSystem>();
		if ( _gridSystem == null )
		{
			Log.Warning( $"{GameObject.Name}: ClutterGridSystem not found in scene" );
			return;
		}

		if ( Isotope == null )
		{
			Log.Warning( $"{GameObject.Name}: No isotope assigned" );
			return;
		}

		if ( Isotope.Scatterer == null )
		{
			Log.Warning( $"{GameObject.Name}: Isotope has no scatterer assigned" );
			return;
		}

		_infiniteData = _gridSystem.Register( Isotope, Isotope.Scatterer, GameObject, TileSize, TileRadius );
		UpdateInfiniteState();
	}

	private void DisableInfinite()
	{
		if ( _infiniteData != null && _gridSystem != null )
		{
			_gridSystem.Unregister( _infiniteData );
			_infiniteData = null;
		}

		_tilesToRegenerate.Clear();
		_isRegenerating = false;
	}

	private void UpdateInfinite()
	{
		if ( _infiniteData == null )
			return;

		if ( _isRegenerating )
		{
			ProcessThrottledRegeneration();
		}
		else if ( CheckInfiniteChanges() )
		{
			StartInfiniteRegeneration();
		}

		if ( UseThisAsCenter )
		{
			_infiniteData.Center = WorldPosition;
		}
	}

	private void OnInfiniteSettingsChanged()
	{
		if ( _infiniteData != null && !_isRegenerating )
		{
			// Update registration immediately
			_infiniteData.TileSize = TileSize;
			_infiniteData.TileRadius = TileRadius;
			
			UpdateInfiniteState();
			StartInfiniteRegeneration();
		}
	}

	private bool CheckInfiniteChanges()
	{
		var currentHash = GetInfiniteHash();
		return currentHash != _lastInfiniteHash;
	}

	private void UpdateInfiniteState()
	{
		_lastInfiniteHash = GetInfiniteHash();
	}

	private int GetInfiniteHash()
	{
		return HashCode.Combine(
			TileSize,
			TileRadius,
			Isotope?.GetHashCode() ?? 0,
			Isotope?.Scatterer?.GetHashCode() ?? 0
		);
	}

	private void StartInfiniteRegeneration()
	{
		if ( _infiniteData == null || _gridSystem == null )
			return;

		_tilesToRegenerate.Clear();
		foreach ( var coord in _infiniteData.Tiles.Keys.ToArray() )
		{
			_tilesToRegenerate.Enqueue( coord );
		}

		_infiniteData.TileSize = TileSize;
		_infiniteData.TileRadius = TileRadius;
		_infiniteData.Isotope = Isotope;
		_infiniteData.Scatterer = Isotope?.Scatterer;

		UpdateInfiniteState();
		_isRegenerating = true;
	}

	private void ProcessThrottledRegeneration()
	{
		if ( _infiniteData == null )
		{
			_isRegenerating = false;
			return;
		}

		int tilesProcessed = 0;
		while ( _tilesToRegenerate.Count > 0 && tilesProcessed < TilesPerFrame )
		{
			var coord = _tilesToRegenerate.Dequeue();

			if ( _infiniteData.Tiles.TryGetValue( coord, out var tile ) )
			{
				tile.Destroy();
				_infiniteData.Tiles.Remove( coord );
			}

			tilesProcessed++;
		}

		if ( _tilesToRegenerate.Count == 0 )
		{
			_isRegenerating = false;
		}
	}
}
