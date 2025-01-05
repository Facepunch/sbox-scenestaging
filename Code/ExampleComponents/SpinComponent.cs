public sealed class SpinComponent : Component
{
	[Property] public Angles SpinAngles { get; set; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		LocalRotation *= (SpinAngles * Time.Delta).ToRotation();
	}
}


public sealed class MoveComponent : Component
{
	[Property] public Vector3 Distance { get; set; }
	[Property] public float Speed { get; set; } = 10.0f;

	Transform startPos;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		startPos = LocalTransform;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		LocalPosition = startPos.Position + (Distance * (MathF.Sin( Time.Now * Speed ).Remap( -1, 1 )));
	}
}


public sealed class MoveSinComponent : Component
{
	[Property] public Vector3 Scale { get; set; } = 100;
	[Property] public float Speed { get; set; } = 1.0f;
	[Property] public float Offset { get; set; } = 0.0f;
	[Property] public float Height { get; set; } = 32.0f;

	Transform startPos;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		startPos = LocalTransform;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();


		Gizmo.Transform = Gizmo.Transform.WithScale( 1 );

		int steps = 100;

		Vector3 pos = default;

		for ( int i = 0; i <= steps; i++ )
		{
			var p = GetPositionAt( i / (float)steps );

			if ( i > 0 )
			{
				Gizmo.Draw.Line( pos, p );
			}

			pos = p;
		}
	}

	Vector3 GetPositionAt( float t )
	{
		var p = startPos.Position;

		p += MathF.Sin( t * MathF.PI * 2.0f + (Offset * MathF.PI * 2.0f) ) * Scale.x * Vector3.Left;
		p += MathF.Cos( t * MathF.PI * 2.0f + (Offset * MathF.PI * 2.0f) ) * Scale.y * Vector3.Forward;

		p += MathF.Sin( t * MathF.PI * 4.0f + (Offset * MathF.PI * 2.0f) ).Remap( -1, 1 ) * Height * Vector3.Up;

		return p;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		var prev = GetPositionAt( (Time.Now * Speed) - 0.01f );

		LocalPosition = GetPositionAt( Time.Now * Speed );
		LocalRotation = Rotation.LookAt( prev - LocalPosition );
	}
}


public sealed class MoveBrownianComponent : Component
{
	[Property] public float Speed { get; set; } = 1.0f;
	[Property] public float Noise { get; set; } = 0.0f;

	Transform startPos;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		startPos = LocalTransform;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Transform = Gizmo.Transform.WithScale( 1 );
		int steps = 100;

		Vector3 pos = default;

		for ( int i = 0; i <= steps; i++ )
		{
			var p = GetPositionAt( i / (float)steps );

			if ( i > 0 )
			{
				Gizmo.Draw.Line( pos, p );
			}

			pos = p;
		}
	}

	Vector3 GetPositionAt( float t )
	{
		var p = startPos.Position;

		var noise = Sandbox.Utility.Noise.FbmVector( 1, 0, t * 512 );

		p += noise.x * Vector3.Left * Noise;
		p += noise.y * Vector3.Forward * Noise;
		p += noise.z * Vector3.Up * Noise * 0.1f;

		return p;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		var prev = GetPositionAt( (Time.Now * Speed) - 0.03f );

		LocalPosition = GetPositionAt( Time.Now * Speed );
		//	LocalRotation = Rotation.LookAt( prev - LocalPosition );
	}
}
