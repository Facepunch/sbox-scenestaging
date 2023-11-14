using Sandbox;
using Sandbox.Engine;
using System.Collections.Generic;

[Title( "Skeletal Pose" )]
[Category( "VR" )]
[Icon( "hand_bones" )]
public class SkeletalPoseComponent : BaseComponent
{
	private AnimatedModelComponent _animatedModelComponent;

	// TODO: These should ideally be user-editable, these values are only for the
	// Alyx hands right now
	private static List<string> AnimGraphNames = new()
	{
		"FingerCurl_Thumb",
		"FingerCurl_Index",
		"FingerCurl_Middle",
		"FingerCurl_Ring",
		"FingerCurl_Pinky"
	};

	public enum HandSources
	{
		Left,
		Right
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
	public HandSources HandSource { get; set; }

	[Property]
	public UpdateTypes UpdateType { get; set; }

	[Property]
	public bool OverrideAnimGraphNames { get; set; }

	public override void OnAwake()
	{
		_animatedModelComponent = GetComponent<AnimatedModelComponent>();
	}

	private void UpdatePose()
	{
		var source = (HandSource == HandSources.Left) ? Input.VR.LeftHand : Input.VR.RightHand;

		_animatedModelComponent.Set( "BasePose", 1 );
		_animatedModelComponent.Set( "bGrab", true );
		_animatedModelComponent.Set( "GrabMode", 1 );

		for ( FingerValue v = FingerValue.ThumbCurl; v <= FingerValue.PinkyCurl; ++v )
		{
			_animatedModelComponent.Set( AnimGraphNames[(int)v], source.GetFingerValue( v ) );
		}
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

		if ( UpdateType.HasFlag( UpdateTypes.OnPreRender ) )
		{
			UpdatePose();
		}
	}
}
