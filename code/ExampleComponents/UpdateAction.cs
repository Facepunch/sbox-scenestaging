using Sandbox;

public class UpdateAction : Component
{
	public delegate Task UpdateFunc( GameObject self, float dt );

	[Property]
	public UpdateFunc Update { get; set; }

	protected override void OnUpdate()
	{
		_ = Update?.Invoke( GameObject, Time.Delta );
	}
}
