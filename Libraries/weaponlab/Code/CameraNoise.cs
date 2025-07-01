using Sandbox.Utility;
using System;
using System.Collections.Generic;

namespace Sandbox.CameraNoise;

public class CameraNoiseSystem : GameObjectSystem<CameraNoiseSystem>, ICameraSetup
{
	List<BaseCameraNoise> _all = new();

	public CameraNoiseSystem( Scene scene ) : base( scene )
	{
	}

	void ICameraSetup.PreSetup( Sandbox.CameraComponent cc )
	{
		foreach ( var effect in _all )
		{
			effect.Update();
		}

		_all.RemoveAll( x => x.IsDone );
	}

	void ICameraSetup.PostSetup( CameraComponent cc )
	{
		foreach ( var effect in _all )
		{
			effect.ModifyCamera( cc );
		}
	}

	public void Add( BaseCameraNoise noise )
	{
		_all.Add( noise );
	}
}

public abstract class BaseCameraNoise
{
	public float LifeTime { get; protected set; }
	public float CurrentTime { get; protected set; }
	public float Delta => CurrentTime.LerpInverse( 0, LifeTime, true );
	public float DeltaInverse => 1 - Delta;

	public BaseCameraNoise()
	{
		CameraNoiseSystem.Current.Add( this );
	}

	public virtual bool IsDone => CurrentTime > LifeTime;

	public virtual void Update()
	{
		CurrentTime += Time.Delta;
	}

	public virtual void ModifyCamera( CameraComponent cc ) { }
}

/// <summary>
/// Creates a bunch of other common effects
/// </summary>
class Recoil : BaseCameraNoise
{
	public Recoil( float amount, float speed = 1 )
	{
		new RollShake() { Size = 0.5f * amount, Waves = 3 * speed };
	}

	public override void ModifyCamera( CameraComponent cc )
	{
	}
}

/// <summary>
/// Shake the screen in a roll motion
/// </summary>
class RollShake : BaseCameraNoise
{
	public float Size { get; set; } = 3.0f;
	public float Waves { get; set; } = 3.0f;

	public RollShake()
	{
		LifeTime = 0.3f;
	}

	public override void ModifyCamera( CameraComponent cc )
	{
		var delta = Delta;
		var s = MathF.Sin( delta * MathF.PI * Waves * 2 );
		cc.WorldRotation *= new Angles( 0, 0, s * Size ) * Easing.EaseOut( DeltaInverse );
	}
}
