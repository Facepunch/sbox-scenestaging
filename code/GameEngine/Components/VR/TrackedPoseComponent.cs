using Sandbox;

[Title( "Tracked Pose" )]
[Category( "VR" )]
[Icon( "front_hand" )]
public class TrackedPoseComponent : BaseComponent
{
	public enum PoseSources
	{
		None,
		Head,
		LeftHand,
		RightHand
	}

	[Flags]
	public enum TrackingTypes
	{
		None,
		Rotation,
		Position,

		All = Rotation | Position
	}

	[Flags]
	public enum UpdateTypes
	{
		None,
		OnPreRender,
		Update,

		All = OnPreRender | Update
	}

	[Property]
	public PoseSources PoseSource { get; set; }

	[Property]
	public TrackingTypes TrackingType { get; set; }

	[Property]
	public UpdateTypes UpdateType { get; set; }

	/// <summary>
	/// Get the appropriate VR transform for the specified <see cref="PoseSource"/>
	/// </summary>
	private Transform GetTransform()
	{
		return PoseSource switch
		{
			PoseSources.Head => Input.VR.Head,
			PoseSources.LeftHand => Input.VR.LeftHand.Transform,
			PoseSources.RightHand => Input.VR.RightHand.Transform,
			_ => new Transform( Vector3.Zero, Rotation.Identity )
		};
	}

	/// <summary>
	/// Set the gameobject's transform based on the <see cref="PoseSource"/> and <see cref="TrackingType"/>
	/// </summary>
	private void UpdatePose()
	{
		var newTransform = GetTransform();

		//
		// Update gameobject transform
		//
		if ( TrackingType.HasFlag( TrackingTypes.Position ) )
			GameObject.Transform.Position = newTransform.Position;

		if ( TrackingType.HasFlag( TrackingTypes.Rotation ) )
			GameObject.Transform.Rotation = newTransform.Rotation;
	}

	public override void Update()
	{
		if ( !Enabled || Scene.IsEditor )
			return;

		if ( UpdateType.HasFlag( UpdateTypes.Update ) )
		{
			UpdatePose();
		}
	}

	protected override void OnPreRender()
	{
		if ( !Enabled || Scene.IsEditor )
			return;

		if ( UpdateType.HasFlag( UpdateTypes.OnPreRender ))
		{
			UpdatePose();
		}
	}
}
