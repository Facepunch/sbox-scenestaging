using Sandbox.MovieMaker;
using Sandbox.Rendering;
using Sandbox.Volumes;
using System.Runtime.CompilerServices;
using static PostProcessSystem;
using static Sandbox.Component;
using static System.Runtime.InteropServices.JavaScript.JSType;

public sealed class PostProcessSystem : GameObjectSystem<PostProcessSystem>, ISceneStage
{

	ConditionalWeakTable<CameraComponent, CameraData> cache = new();

	public class CameraData
	{
		public CameraData()
		{

		}

		public CameraData( CameraComponent cc )
		{
			Camera = cc;
		}

		public CameraComponent Camera { get; set; }
		public List<WeightedEffect> Effects { get; set; } = new();

		Dictionary<Sandbox.Rendering.Stage, CommandList> commands = new();

		public CommandList Get( Sandbox.Rendering.Stage stage )
		{
			if ( commands.TryGetValue( stage, out var cl ) )
				return cl;

			commands[stage] = cl = new CommandList( $"Post Process - {stage}" );
			Camera.AddCommandList( cl, stage, 0 );
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

	void ISceneStage.End()
	{
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
	public WeightedEffect[] Components;

	public void Add( CommandList cl, Sandbox.Rendering.Stage stage, int order = 0 )
	{
		var bucket = _context.Get( stage );
		bucket.InsertList( cl );
	}

	public float GetBlended<T>( Func<T, float> value, float defaultVal = 0 ) where T: BasePostProcess
	{
		float v = defaultVal;

		foreach ( var e in Components )
		{
			var target = value( (T)e.Effect );
			v = v.LerpTo( target, e.Weight );
		}

		return v;
	}

	public U GetBlended<T, U>( Func<T, U> value, U defaultVal = default ) where T : BasePostProcess
	{
		U v = defaultVal;
		var lerper = Interpolator.GetDefault<U>();

		foreach ( var e in Components )
		{
			var target = value( (T)e.Effect );
			v = lerper.Interpolate( v, target, e.Weight );
		}

		return v;
	}
}
