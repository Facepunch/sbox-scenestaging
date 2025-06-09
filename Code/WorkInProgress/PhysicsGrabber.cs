
using Sandbox.Physics;

public class PhysicsGraber : Component
{
	private PhysicsBody GrabbedBody;
	private GameObject GrabbedObject;

	private Vector3 GrabbedAimLocal;
	private Vector3 GrabbedObjectLocal;

	private PhysicsBody GrabBody;
	private Sandbox.Physics.FixedJoint GrabJoint;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		Clear();

		GrabBody = new PhysicsBody( Scene.PhysicsWorld )
		{
			BodyType = PhysicsBodyType.Keyframed
		};
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		Clear();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		Clear();
	}

	private void Clear()
	{
		GrabJoint?.Remove();
		GrabJoint = null;

		GrabBody?.Remove();
		GrabBody = null;

		GrabbedBody = null;
		GrabbedObject = null;
		GrabbedAimLocal = default;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( GrabbedBody.IsValid() )
		{
			if ( !Input.Down( "attack1" ) )
			{
				GrabJoint?.Remove();
				GrabJoint = null;

				GrabbedBody = null;
				GrabbedObject = null;
				GrabbedAimLocal = default;
			}
			else
			{
				return;
			}
		}

		var tr = Scene.Trace.Ray( Scene.Camera.WorldPosition, Scene.Camera.WorldPosition + Scene.Camera.WorldRotation.Forward * 1000 )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();

		if ( !tr.Hit || tr.Body is null )
			return;

		if ( tr.Body.BodyType == PhysicsBodyType.Static )
			return;

		if ( Input.Down( "attack1" ) )
		{
			var aimTransform = Scene.Camera.WorldTransform;

			GrabbedBody = tr.Body;
			GrabbedObject = tr.GameObject;
			GrabbedObjectLocal = GrabbedObject.WorldTransform.PointToLocal( tr.HitPosition );

			var localOffset = GrabbedBody.Transform.PointToLocal( tr.HitPosition );

			GrabbedAimLocal = aimTransform.PointToLocal( tr.HitPosition );
			GrabBody.Position = tr.HitPosition;

			GrabJoint?.Remove();
			GrabJoint = PhysicsJoint.CreateFixed( new PhysicsPoint( GrabBody ), new PhysicsPoint( GrabbedBody ) );
			GrabJoint.Point1 = new PhysicsPoint( GrabBody );
			GrabJoint.Point2 = new PhysicsPoint( GrabbedBody, localOffset );

			var maxForce = 100.0f * tr.Body.Mass * Scene.PhysicsWorld.Gravity.Length;
			GrabJoint.SpringLinear = new PhysicsSpring( 15, 1, maxForce );
			GrabJoint.SpringAngular = new PhysicsSpring( 0, 0, 0 );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		if ( !GrabbedBody.IsValid() )
			return;

		if ( !GrabBody.IsValid() )
			return;

		var aimTransform = Scene.Camera.WorldTransform;
		GrabBody.Position = aimTransform.PointToWorld( GrabbedAimLocal );
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();

		if ( !GrabbedObject.IsValid() )
		{
			var tr = Scene.Trace.Ray( Scene.Camera.ScreenNormalToRay( 0.5f ), 1000.0f )
						.IgnoreGameObjectHierarchy( GameObject.Root )
						.Run();

			if ( tr.Hit )
			{
				Gizmo.Draw.Color = Color.Cyan;
				Gizmo.Draw.SolidSphere( tr.HitPosition, 1 );
			}
		}
		else
		{
			var position = GrabbedObject.WorldTransform.PointToWorld( GrabbedObjectLocal );

			Gizmo.Draw.Color = Color.Cyan;
			Gizmo.Draw.SolidSphere( position, 1 );
		}
	}
}
