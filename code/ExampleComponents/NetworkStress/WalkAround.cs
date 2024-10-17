public class WalkAround : Component
{
	private Vector3 CurrentDirection { get; set; }
	private TimeUntil NextChangeDirection { get; set; }
	
	protected override void OnFixedUpdate()
	{
		if ( !IsProxy )
		{
			if ( NextChangeDirection )
			{
				CurrentDirection = Vector3.Random.WithZ( 0f );
				NextChangeDirection = Game.Random.Float( 2f, 8f );
			}

			var pos = WorldPosition;
			pos += CurrentDirection * 32f * Time.Delta;
			WorldPosition = pos;
		}
		
		base.OnFixedUpdate();
	}
}
