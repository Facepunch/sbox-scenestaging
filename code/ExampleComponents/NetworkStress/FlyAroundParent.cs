public class FlyAroundParent : Component
{
	private Vector3 Direction { get; set; }
	
	protected override void OnStart()
	{
		if ( !IsProxy )
		{
			Direction = Vector3.Random;
		}
		
		base.OnStart();
	}

	protected override void OnFixedUpdate()
	{
		if ( !IsProxy )
		{
			var localPos = LocalPosition;
			localPos.x = Direction.x * MathF.Sin( Time.Now ) * 64f;
			localPos.y = Direction.y * MathF.Cos( Time.Now ) * 64f;
			localPos.z = 80f + MathF.Sin( Time.Now ) * 8f + MathF.Cos( Time.Now ) * 8f;
			LocalPosition = localPos;
		}
		
		base.OnFixedUpdate();
	}
}
