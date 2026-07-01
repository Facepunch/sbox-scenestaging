using Sandbox;

/// <summary>
/// Click in a 2D scene to detonate an explosion. Finds every physics body within
/// <see cref="Radius"/> using <see cref="Scene.FindInPhysics(Sphere)"/> and pushes it
/// away from the blast with a linear distance falloff.
/// </summary>
public sealed class Explosion2D : Component
{
	[Property] public float Radius { get; set; } = 200f;

	[Property] public float Force { get; set; } = 1000000f;

	[Property] public string InputAction { get; set; } = "attack1";

	[Property] public GameObject EffectPrefab { get; set; }

	protected override void OnEnabled()
	{
		Mouse.Visible = true;
	}

	protected override void OnUpdate()
	{
		Mouse.Visible = true;

		if ( !Input.Pressed( InputAction ) )
			return;

		if ( TryGetMouseWorld( out var point ) )
			Detonate( point );
	}

	public void Detonate( Vector3 point )
	{
		if ( EffectPrefab.IsValid() )
			EffectPrefab.Clone( point );

		foreach ( var target in Scene.FindInPhysics( new Sphere( point, Radius ) ) )
		{
			var body = target.Components.Get<Rigidbody>();
			if ( !body.IsValid() )
				continue;

			var offset = body.WorldPosition - point;
			var falloff = MathF.Sqrt( 1f - Math.Clamp( offset.Length / Radius, 0f, 1f ) );
			body.ApplyImpulseAt( point, offset.Normal * Force * falloff );
		}
	}

	bool TryGetMouseWorld( out Vector3 point )
	{
		point = default;

		var camera = Scene.Camera;
		if ( !camera.IsValid() )
			return false;

		var ray = camera.ScreenPixelToRay( Mouse.Position );
		if ( ray.Forward.z.AlmostEqual( 0f ) )
			return false;

		var distance = -ray.Position.z / ray.Forward.z;
		point = ray.Position + ray.Forward * distance;
		return true;
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.LineSphere( 0, Radius );
	}
}
