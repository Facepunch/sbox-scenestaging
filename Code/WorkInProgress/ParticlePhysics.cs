public sealed class ParticlePhysics : ParticleController
{
	protected override void OnParticleCreated( Particle p )
	{
		var body = new PhysicsBody( Scene.PhysicsWorld );
		body.Position = p.Position;

		//	body.AddBoxShape( BBox.FromPositionAndSize( 0, 10 ), Rotation.Identity );

		var shape = body.AddSphereShape( new Sphere( 0, 10 ) );

		shape.Tags.Add( "particle" );
		shape.EnableTouch = false;
		shape.EnableTouchPersists = false;

		body.EnableCollisionSounds = false;

		body.GravityEnabled = true;
		body.MotionEnabled = true;
		body.Velocity = p.Velocity * 2;
		body.Mass = 100;
		body.RebuildMass();
		body.Sleeping = false;

		p.Set( "Physics", body );
	}

	protected override void OnParticleStep( Particle particle, float delta )
	{
		var body = particle.Get<PhysicsBody>( "Physics" );
		if ( body is null ) return;

		particle.Position = body.Position;
		particle.Angles = body.Rotation;
	}

	protected override void OnParticleDestroyed( Particle p )
	{
		var body = p.Get<PhysicsBody>( "Physics" );
		if ( body is not null )
		{
			body.Remove();
		}

		p.Set<PhysicsBody>( "Physics", null );
	}



}
