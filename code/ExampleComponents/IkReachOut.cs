using Sandbox;

public sealed class IkReachOut : Component
{
	[Property] public GameObject TargetGameObject { get; set; }
	[Property] public float Radius { get; set; }
	[Property] public Angles HandRotation { get; set; }
	[Property] public TagSet IgnoreCollision { get; set; }

	TimeUntil timeUntilRetry;

	protected override void OnUpdate()
	{
		if ( timeUntilRetry > 0 )
			return;

		var dir = (TargetGameObject.Transform.Position- Transform.Position);
		if ( !TargetGameObject.Enabled )
		{
			dir = (Transform.Rotation.Forward + Vector3.Random) * Radius;
		}

		var tr = Scene.Trace
			.Sphere( 2, Transform.Position, Transform.Position + dir.Normal * Radius )
			.WithoutTags( IgnoreCollision )
			.Run();

		if  ( tr.Hit )
		{
			TargetGameObject.Transform.Position = tr.EndPosition;
			TargetGameObject.Transform.Rotation = Rotation.LookAt( tr.Normal ) * Rotation.From( HandRotation );
			TargetGameObject.Enabled = true;
		}
		else
		{
			if ( TargetGameObject.Enabled )
				timeUntilRetry = 0.5f;

			TargetGameObject.Enabled = false;
		}
	}
}
