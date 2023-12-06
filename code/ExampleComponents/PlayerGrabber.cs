using Sandbox;
using Sandbox.Physics;
using System.Drawing;
using System.Runtime;

public class PlayerGrabber : Component
{
	PhysicsBody grabbedBody;
	Transform grabbedOffset;
	Vector3 localOffset;

	bool waitForUp = false;

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		var cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();

		Transform aimTransform = cam.Transform.World;

		if ( waitForUp && Input.Down( "attack1" ) )
		{
			return;
		}

		waitForUp = false;

		if ( grabbedBody is not null )
		{
			if ( Input.Down( "attack2" ) )
			{
				grabbedBody.MotionEnabled = false;
				grabbedBody.Velocity = 0;
				grabbedBody.AngularVelocity = 0;

				grabbedOffset = default;
				grabbedBody = default;
				waitForUp = true;
				return;
			}

			var targetTx = aimTransform.ToWorld( grabbedOffset );

			var worldStart = grabbedBody.GetLerpedTransform( Time.Now ).PointToWorld( localOffset );
			var worldEnd = targetTx.Position;

			var delta = worldEnd - worldStart;
			for ( var f = 0.0f; f < delta.Length; f += 1.0f )
			{
				Gizmo.Draw.SolidSphere( worldStart + delta.Normal * f, 2 );
			}

			if ( !Input.Down( "attack1" ) )
			{
				grabbedOffset = default;
				grabbedBody = default;
			}
			else
			{
				grabbedBody.SmoothMove( targetTx, 0.2f, Time.Delta );
				return;
			}
		}

		if ( Input.Down( "attack2" ) )
			return;

		var tr = Scene.Trace.Ray( cam.Transform.Position, cam.Transform.Position + cam.Transform.Rotation.Forward * 1000 )
			.Run();

		if ( tr.Hit )
		{
			Gizmo.Draw.SolidSphere( tr.HitPosition, 2 );
		}

		if ( !tr.Hit || tr.Body is null || tr.Body.BodyType == PhysicsBodyType.Static )
			return;

		if ( Input.Down( "attack1" ) )
		{
			grabbedBody = tr.Body;
			localOffset = tr.Body.Transform.PointToLocal( tr.HitPosition );
			grabbedOffset = aimTransform.ToLocal( tr.Body.Transform );
			grabbedBody.MotionEnabled = true;
		}

	}
}
