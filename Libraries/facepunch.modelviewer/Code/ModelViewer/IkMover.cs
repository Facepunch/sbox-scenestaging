using Sandbox;

public sealed class IkMover : Component
{
	[Property] public GameObject TargetGameObject { get; set; }
	public Vector3 Offset { get; set; }
	public Rotation RotationOffset { get; set; }

	protected override void OnUpdate()
	{
		TargetGameObject.WorldPosition = Offset;
		TargetGameObject.WorldRotation = RotationOffset;
	}
}
