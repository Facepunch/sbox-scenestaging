
namespace Sandbox;

/// <summary>
/// Ticks the physics in FrameStage.PhysicsStep
/// </summary>
class HitboxSystem : GameObjectSystem, GameObjectSystem.ITraceProvider
{
	public PhysicsWorld PhysicsWorld { get; private set; }

	public HitboxSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.UpdateBones, 100, InvalidateHitboxes, "InvalidateHitboxes" );

		PhysicsWorld = new PhysicsWorld();
	}

	public override void Dispose()
	{
		PhysicsWorld.Delete();
		PhysicsWorld = null;
	}

	bool hitboxesDirty;

	void InvalidateHitboxes()
	{
		hitboxesDirty = true;
	}

	void UpdateHitboxPositions()
	{
		if ( !hitboxesDirty )
			return;

		lock ( this )
		{
			if ( !hitboxesDirty )
				return;

			hitboxesDirty = false;

			// these could be foreach parallel!!

			foreach ( var group in Scene.GetAllComponents<ModelHitboxes>() )
			{
				group.UpdatePositions();
			}

			foreach ( var group in Scene.GetAllComponents<ManualHitbox>() )
			{
				group.UpdatePositions();
			}
		}
	}

	public void DoTrace( in SceneTrace trace, List<SceneTraceResult> results )
	{
		if ( !trace.IncludeHitboxes )
			return;

		UpdateHitboxPositions();

		var tr = PhysicsWorld.RunTrace( trace.PhysicsTrace );

		if ( !tr.Hit )
			return;

		var result = SceneTraceResult.From( Scene, tr );
		result.Hitbox = (global::Hitbox) tr.Body.GameObject;
		results.Add( result );
	}
}
