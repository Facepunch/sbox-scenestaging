using Sandbox;
using Sandbox.Rendering;
using System.Collections.Generic;

namespace SceneStaging;

/// <summary>
/// Manages imposter LOD switching using spatial grid queries.
/// All imposters batched into single sprite batch for performance.
/// </summary>
public class ImposterSystem : GameObjectSystem<ImposterSystem>, ISpriteRenderGroup
{
	private SpatialGrid _spatialGrid;
	private SceneSpriteSystem _spriteSystem;
	private Dictionary<ImposterComponent, bool> _states = new();
	private List<SpriteBatchSceneObject.SpriteData> _sprites = new();
	private SpriteBatchSceneObject.SpriteData[] _spriteArray;
	private HashSet<ImposterComponent> _processed = new();

	public bool Opaque => true;
	public bool Additive => false;
	public bool Shadows => false;
	public bool IsSorted => false;
	public float MaxQueryDistance { get; set; } = 10000f;

	public ImposterSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, OnUpdate, "ImposterUpdate" );
	}

	private void OnUpdate()
	{
		_spatialGrid ??= Scene.GetSystem<SpatialGrid>();
		_spriteSystem ??= Scene.GetSystem<SceneSpriteSystem>();
		if ( _spatialGrid == null || _spriteSystem == null || Scene.Camera == null ) return;

		_sprites.Clear();
		_processed.Clear();

		var cameraPos = Scene.Camera.WorldPosition;
		var nearby = _spatialGrid.Query<ImposterComponent>( cameraPos, MaxQueryDistance );

		// Process nearby imposters
		foreach ( var imposter in nearby )
		{
			if ( !IsValid( imposter ) ) continue;

			_processed.Add( imposter );
			var distance = Vector3.DistanceBetween( cameraPos, imposter.GameObject.WorldPosition );
			var showImposter = imposter.ForceShowImposter || distance >= imposter.ImposterDistance;

			// Initialize or update state
			if ( !_states.TryGetValue( imposter, out var wasShowingImposter ) )
			{
				SetRenderers( imposter, !showImposter );
				_states[imposter] = showImposter;
			}
			else if ( wasShowingImposter != showImposter )
			{
				SetRenderers( imposter, !showImposter );
				_states[imposter] = showImposter;
			}

			if ( showImposter )
				_sprites.Add( BuildSprite( imposter ) );
		}

		// Add distant imposters still showing
		foreach ( var (imposter, isShowing) in _states )
		{
			if ( !IsValid( imposter ) || _processed.Contains( imposter ) ) continue;
			if ( isShowing )
				_sprites.Add( BuildSprite( imposter ) );
		}

		// Cleanup destroyed
		_states.RemoveAll( kvp => !IsValid( kvp.Key ) );

		// Update batch
		if ( _sprites.Count > 0 )
		{
			if ( _spriteArray == null || _spriteArray.Length != _sprites.Count )
				_spriteArray = new SpriteBatchSceneObject.SpriteData[_sprites.Count];

			_sprites.CopyTo( _spriteArray );
			_spriteSystem.RegisterSpriteBatch( Id, _spriteArray, this );
		}
		else
		{
			_spriteSystem.UnregisterSpriteBatch( Id );
		}
	}

	private bool IsValid( ImposterComponent imp ) =>
		imp != null && imp.IsValid && imp.Enabled && imp.GameObject != null && imp.ImposterAsset?.ColorAtlas != null;

	private void SetRenderers( ImposterComponent imposter, bool enabled )
	{
		foreach ( var r in imposter.Renderers )
			if ( r != null && r.IsValid ) r.Enabled = enabled;
	}

	private SpriteBatchSceneObject.SpriteData BuildSprite( ImposterComponent imp )
	{
		var bounds = imp.ImposterAsset.Bounds;
		var size = bounds.Size.Length * 0.5f * 1.02f * imp.SizeMultiplier;
		if ( size <= 0f ) size = 100f;

		var lighting = (uint)(((imp.Lighting ? 1 : 0)) | ((imp.Lighting ? 128 : 0) << 16));
		var normal = imp.ImposterAsset.NormalAtlas?.Index ?? 0;

		return new SpriteBatchSceneObject.SpriteData
		{
			Position = imp.GameObject.WorldPosition + imp.ImposterAsset.PivotOffset,
			Scale = new Vector2( size, size ),
			TintColor = SpriteBatchSceneObject.SpriteData.PackColor( imp.Tint ),
			TextureHandle = imp.ImposterAsset.ColorAtlas.Index,
			RenderFlags = 0x8,
			BillboardMode = 3,
			FogStrength = 1f,
			Lighting = lighting,
			Splots = normal,
			RotationOffset = -1f,
			Offset = new Vector2( 0.5f, 1f )
		};
	}
}
