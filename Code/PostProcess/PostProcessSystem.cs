using Sandbox.MovieMaker;
using Sandbox.Rendering;
using Sandbox.Utility;
using Sandbox.Volumes;
using System.Runtime.CompilerServices;
namespace Sandbox;

public sealed partial class PostProcessSystem : GameObjectSystem<PostProcessSystem>, Component.ISceneStage, Component.IRenderThread
{

	ConditionalWeakTable<CameraComponent, PostProcessCamera> cache = new();



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
		PostProcessCamera data = GetOrAdd( cc );
		data.Clear();

		var pos = cc.WorldPosition;

		data.Effects.AddRange( cc.GetComponentsInChildren<BasePostProcess>().Select( x => new WeightedEffect {  Effect = x, Weight = 1 } ) );

		var volumes = Scene.GetSystem<VolumeSystem>()?.FindAll<PostProcessVolume>( pos );
		foreach ( var volume in volumes.OrderBy( x => x.Priority ) )
		{
			var weight = volume.GetWeight( pos );
			data.Effects.AddRange( volume.GetComponentsInChildren<BasePostProcess>().Select( x => new WeightedEffect { Effect = x, Weight = weight } ) );
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
	}

	private void UpdateCamera( PostProcessVolume volume )
	{
		PostProcessCamera data = GetOrAdd( Scene.Camera );
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

	private PostProcessCamera GetOrAdd( CameraComponent cc )
	{
		if ( !cache.TryGetValue( cc, out PostProcessCamera data ) )
		{
			data = new PostProcessCamera( cc );

			cache.Add( cc, data );
		}

		return data;
	}

	public void OnRenderStage( CameraComponent camera, Sandbox.Rendering.Stage stage )
	{
		if ( !cache.TryGetValue( camera, out PostProcessCamera data ) )
			return;

		data.OnRenderStage( stage );
	}
}

internal struct PostProcessContext
{
	internal PostProcessCamera _context;
	public CameraComponent Camera => _context.Camera;
	public WeightedEffect[] Components;

	public void Add( CommandList cl, Sandbox.Rendering.Stage stage, int order = 0 )
	{
		var layer = _context.Get( stage, order );
		layer.CommandList = cl;
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
