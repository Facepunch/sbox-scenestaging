using Sandbox;
using Sandbox.Diagnostics;

[Title( "Collider - Sphere" )]
[Category( "Physics" )]
[Icon( "panorama_fish_eye", "red", "white" )]
[Alias( "SphereColliderComponent" )]
public class ColliderSphereComponent : GameObjectComponent
{
	[Property] public float Radius { get; set; } = 10.0f;
	[Property] public Surface Surface { get; set; }

	public override void DrawGizmos()
	{
		Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsChildSelected ? 0.5f : 0.1f );
		Gizmo.Draw.LineSphere( new Sphere( 0, Radius ) );
	}

	public override void OnDisabled()
	{

	}

	PhysicsShape shape;
	PhysicsBody ownBody;

	public override void OnEnabled()
	{

		Assert.IsNull( ownBody );
		Assert.IsNull( shape );

		var tx = GameObject.WorldTransform;
		PhysicsBody physicsBody = null;

		// is there a physics body?
		var body = GameObject.GetComponentInParent<PhysicsComponent>();
		if ( body is not null )
		{
			physicsBody = body.GetBody();

			//
			if ( physicsBody is null )
			{
				Log.Warning( $"{this}: PhysicsBody from {body} was null" );
				return;
			}

		}

		if ( physicsBody is null )
		{
			physicsBody = new PhysicsBody( Scene.PhysicsWorld );
			physicsBody.BodyType = PhysicsBodyType.Static;
			physicsBody.GameObject = GameObject;
			physicsBody.Transform = GameObject.WorldTransform;
			physicsBody.GravityEnabled = false;
			ownBody = physicsBody;
		}

		tx = physicsBody.Transform.ToLocal( tx );

		// todo position relative to body
		shape = physicsBody.AddSphereShape( tx.Position, Radius * tx.Scale );
		if ( shape is null ) return;

		shape.AddTag( "solid" );

		if ( Surface is not null )
		{
			shape.SurfaceMaterial = Surface.ResourceName;
		}
	}
}
