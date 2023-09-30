using Sandbox;
using Sandbox.Diagnostics;

[Title( "Collider - Box" )]
[Category( "Physics" )]
[Icon( "check_box_outline_blank", "red", "white" )]
public class ColliderBoxComponent : GameObjectComponent, PhysicsComponent.IBodyModifier
{
	[Property] public Vector3 Scale { get; set; } = 50;
	[Property] public Surface Surface { get; set; }

	PhysicsShape shape;
	PhysicsBody ownBody;

	public override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected && !Gizmo.IsHovered )
			return;

		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsSelected ? 1.0f : 0.2f );
		Gizmo.Draw.LineBBox( new BBox( Scale * -0.5f, Scale * 0.5f ) );
	}

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
			physicsBody.BodyType = PhysicsBodyType.Keyframed;
			physicsBody.UseController = true;
			physicsBody.GameObject = GameObject;
			physicsBody.Transform = GameObject.WorldTransform;
			physicsBody.GravityEnabled = false;
			ownBody = physicsBody;
		}

		tx = physicsBody.Transform.ToLocal( tx );

		// todo position relative to body
		shape = physicsBody.AddBoxShape( tx.Position, tx.Rotation, Scale * 0.5f * tx.Scale );
		if ( shape is null ) return;

		shape.AddTag( "solid" );

		if ( Surface is not null )
		{
			shape.SurfaceMaterial = Surface.ResourceName;
		}
	}

	public override void OnDisabled()
	{
		//shape?.Body?.RemoveShape( shape );
		shape = null;

		ownBody?.Remove();
		ownBody = null;
	}

	public void ModifyBody( PhysicsBody body )
	{

	}

	protected override void OnPreRender()
	{
		if ( ownBody  is not null )
		{
			//GameObject.Transform = ownBody.Transform;
		}
		
	}

	protected override void OnPostPhysics()
	{
		if ( ownBody is not null )
		{
			ownBody.Transform = GameObject.WorldTransform;
		}
	}
}
