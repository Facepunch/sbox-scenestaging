using Sandbox;

/// <summary>
/// Updates this GameObject's transform based on a given tracked object (e.g. left controller, HMD).
/// </summary>
[Title( "VR Tracked Object" )]
[Category( "VR" )]
[Icon( "animation" )]
public class VrTrackedObject : BaseComponent
{
	public enum PoseSources
	{
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

	/// <summary>
	/// Which tracked object should we use to update the transform?
	/// </summary>
	[Property]
	public PoseSources PoseSource { get; set; } = PoseSources.Head;

	/// <summary>
	/// Which parts of the transform should be updated? (eg. rotation, position)
	/// </summary>
	[Property]
	public TrackingTypes TrackingType { get; set; } = TrackingTypes.All;

	/// <summary>
	/// If this is checked, then the transform used will be relative to the VR anchor (rather than an absolute world position).
	/// </summary>
	[Property]
	public bool UseRelativeTransform { get; set; } = false;

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
	/// Set the GameObject's transform based on the <see cref="PoseSource"/> and <see cref="TrackingType"/>
	/// </summary>
	private void UpdatePose()
	{
		var newTransform = GetTransform();

		if ( UseRelativeTransform )
			newTransform = VR.Anchor.ToLocal( newTransform );

		//
		// Update GameObject transform
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

		UpdatePose();
	}

	protected override void OnPreRender()
	{
		if ( !Enabled || Scene.IsEditor )
			return;

		UpdatePose();
	}
}
