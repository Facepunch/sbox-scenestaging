using Sandbox;

public sealed class ComputePenetrationDemo : Component
{
	[Property] public Collider Target { get; set; }
	[Property] public string Label { get; set; }
	[Property] public float Speed { get; set; } = 1.5f;
	[Property] public float Range { get; set; } = 60f;

	Collider self;
	Vector3 origin;

	protected override void OnStart()
	{
		self = GetComponent<Collider>();
		origin = WorldPosition;
	}

	protected override void OnUpdate()
	{
		if ( !self.IsValid() || !Target.IsValid() )
			return;

		WorldPosition = origin + Vector3.Forward * MathF.Sin( Time.Now * Speed ) * Range;

		var overlapping = self.ComputePenetration( Target, out var direction, out var distance );

		DrawShape( Target, Target.WorldTransform, Color.White.WithAlpha( 0.4f ) );
		DrawShape( self, WorldTransform, overlapping ? Color.Red : Color.Green );

		if ( !string.IsNullOrEmpty( Label ) )
			DebugOverlay.Text( origin + Vector3.Up * 110, Label, size: 36 );

		if ( !overlapping )
			return;

		var resolved = WorldPosition + direction * distance;

		DrawShape( self, new Transform( resolved, WorldRotation, WorldScale ), Color.Cyan.WithAlpha( 0.5f ) );
		DebugOverlay.Line( WorldPosition, resolved, Color.Yellow );
		DebugOverlay.Sphere( new Sphere( resolved, 3 ), Color.Yellow );
		DebugOverlay.Text( origin + Vector3.Down * 75, $"{distance:0.0}u  {direction:0.00}", size: 30 );
	}

	void DrawShape( Collider collider, Transform transform, Color color )
	{
		switch ( collider )
		{
			case SphereCollider s:
				DebugOverlay.Sphere( new Sphere( s.Center, s.Radius ), color, transform: transform );
				break;
			case CapsuleCollider c:
				DebugOverlay.Capsule( new Capsule( c.Start, c.End, c.Radius ), color, transform: transform );
				break;
			default:
				DebugOverlay.Box( collider.LocalBounds, color, transform: transform );
				break;
		}
	}
}
