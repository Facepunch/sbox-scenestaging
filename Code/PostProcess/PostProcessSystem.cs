using Sandbox.MovieMaker;
using Sandbox.Rendering;
using Sandbox.Utility;
using Sandbox.Volumes;
using System.Runtime.CompilerServices;

public sealed class PostProcessSystem : GameObjectSystem<PostProcessSystem>, Component.ISceneStage
{

	ConditionalWeakTable<CameraComponent, CameraData> cache = new();

	internal class CameraData
	{
		internal CameraData( CameraComponent cc )
		{
			Camera = cc;
		}

		public CameraComponent Camera { get; set; }
		public List<WeightedEffect> Effects { get; set; } = new();

		Dictionary<(Sandbox.Rendering.Stage stage, int order), CommandList> commands = new();

		public CommandList Get( Sandbox.Rendering.Stage stage, int order )
		{
			if ( commands.TryGetValue( (stage, order), out var cl ) )
				return cl;

			commands[(stage, order)] = cl = new CommandList( $"Post Process - {stage}" );
			cl.Flags |= CommandList.Flag.PostProcess;
			Camera.AddCommandList( cl, stage, order );
			return cl;
		}

		internal void Clear()
		{
			Effects.Clear();

			foreach( var c in commands.Values )
			{
				c.Reset();
			}
		}
	}

	public record struct WeightedEffect
	{
		public BasePostProcess Effect;
		public float Weight;
	}

	public PostProcessSystem( Scene scene ) : base( scene )
	{
	
	}

	void UpdateEditorScene()
	{
		if ( Scene.Camera is null )
			return;

		Scene.Camera.AutoExposure.Enabled = true;
		Scene.Camera.AutoExposure.Compensation = 0;
		Scene.Camera.AutoExposure.Rate = 20;
		Scene.Camera.AutoExposure.MinimumExposure = 1;
		Scene.Camera.AutoExposure.MaximumExposure = 2;

		// Clear all
		foreach ( var v in cache )
		{
			v.Value.Clear();
		}

		if ( Scene.Editor.SelectedGameObject is GameObject go )
		{
			if ( go.GetComponentInParent<CameraComponent>( false, true ) is CameraComponent cc )
			{
				UpdateCamera( cc );
				return;
			}

			if ( go.GetComponentInParent<PostProcessVolume>( false, true ) is PostProcessVolume volume && volume.EditorPreview )
			{
				UpdateCamera( volume );
				return;
			}
		}
	}

	void Component.ISceneStage.End()
	{
		if ( Scene.IsEditor )
		{
			UpdateEditorScene();
			return;
		}

		foreach ( var cc in Scene.GetAll<CameraComponent>() )
		{
			UpdateCamera( cc );
		}
	}

	private void UpdateCamera( CameraComponent cc )
	{
		CameraData data = GetOrAdd( cc );
		data.Clear();

		var text = $"{cc.GameObject} / {cc.WorldPosition}\n";

		var pos = cc.WorldPosition;

		data.Effects.AddRange( cc.GetComponentsInChildren<BasePostProcess>().Select( x => new WeightedEffect {  Effect = x, Weight = 1 } ) );

		var volumes = Scene.GetSystem<VolumeSystem>()?.FindAll<PostProcessVolume>( pos );
		foreach ( var volume in volumes.OrderBy( x => x.Priority ) )
		{
			text += $" VOLUME: {volume} ({volume.WorldPosition}, {volume.WorldScale})\n";

			var weight = volume.GetWeight( pos ) * volume.Weight.Clamp( 0, 1 );

			data.Effects.AddRange( volume.GetComponentsInChildren<BasePostProcess>().Select( x => new WeightedEffect { Effect = x, Weight = weight } ) );
		}

		foreach ( var group in data.Effects.GroupBy( x => x.Effect.GetType() ) )
		{
			text += $" EFFECT: {group.Key}\n";

			foreach ( var effect in group )
			{
				text += $"    {effect.Effect.GameObject} - {effect.Weight} \n";
			}
		}

		foreach ( var group in data.Effects.GroupBy( x => x.Effect.GetType() ) )
		{
			var effect = group.First();

			var ctx = new PostProcessContext()
			{
				_context = data,
				Components = group.ToArray()
			};

			effect.Effect.Build( ctx );
		}

		Scene.DebugOverlay.ScreenText( 200, text, flags: TextFlag.LeftTop );
	}

	private void UpdateCamera( PostProcessVolume volume )
	{
		CameraData data = GetOrAdd( Scene.Camera );
		data.Clear();

		var pos = volume.WorldPosition;

		data.Effects.AddRange( volume.GetComponentsInChildren<BasePostProcess>().Select( x => new WeightedEffect { Effect = x, Weight = 1 } ) );

		foreach ( var group in data.Effects.GroupBy( x => x.Effect.GetType() ) )
		{
			var effect = group.First();

			var ctx = new PostProcessContext()
			{
				_context = data,
				Components = group.ToArray()
			};

			effect.Effect.Build( ctx );
		}
	}

	private CameraData GetOrAdd( CameraComponent cc )
	{
		if ( !cache.TryGetValue( cc, out CameraData data ) )
		{
			data = new CameraData( cc );

			cache.Add( cc, data );
		}

		return data;
	}
}

public ref struct PostProcessContext
{
	internal PostProcessSystem.CameraData _context;
	public CameraComponent Camera => _context.Camera;
	public PostProcessSystem.WeightedEffect[] Components;

	public void Add( CommandList cl, Sandbox.Rendering.Stage stage, int order = 0 )
	{
		var bucket = _context.Get( stage, order );
		bucket.InsertList( cl );
	}

	public float GetBlended<T>( Func<T, float> value, float defaultVal = 0, bool onlyLerpBetweenVolumes = false ) where T: BasePostProcess
	{
		float v = defaultVal;

		int i = 0;
		foreach ( var e in Components )
		{
			var target = value( (T)e.Effect );
			v = v.LerpTo( target, e.Weight );

			if ( onlyLerpBetweenVolumes && i == 0 )
				v = target;

			i++;
		}

		return v;
	}

	public U GetBlended<T, U>( Func<T, U> value, U defaultVal = default, bool onlyLerpBetweenVolumes = false ) where T : BasePostProcess
	{
		U v = defaultVal;
		var lerper = Interpolator.GetDefault<U>();

		int i = 0;
		foreach ( var e in Components )
		{
			var target = value( (T)e.Effect );
			v = lerper.Interpolate( v, target, e.Weight );

			if ( onlyLerpBetweenVolumes && i == 0 )
				v = target;

			i++;
		}

		return v;
	}
}
