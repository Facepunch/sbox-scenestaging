using Sandbox;
using Sandbox.Engine;
using System.Collections.Generic;

/// <summary>
/// Updates the parameters on an <see cref="AnimatedModelComponent"/> on this GameObject based on the skeletal data from SteamVR.
/// Useful for quick hand posing based on controller input.
/// </summary>
[Title( "VR Hand" )]
[Category( "VR" )]
[Icon( "waving_hand" )]
public class VrHand : BaseComponent
{
	private AnimatedModelComponent _animatedModelComponent;

	// TODO: These should ideally be user-editable, these values only work on the Alyx hands right now
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

	/// <summary>
	/// Which hand should we use to update the parameters?
	/// </summary>
	[Property]
	public HandSources HandSource { get; set; } = HandSources.Left;

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

		UpdatePose();
	}

	protected override void OnPreRender()
	{
		if ( !Enabled || Scene.IsEditor )
			return;

		UpdatePose();
	}
}
