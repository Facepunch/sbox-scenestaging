using Sandbox;

/// <summary>
/// VR 玩家根節點：集中啟用／停用位移、桌面模擬與雙手抓取，並可選自動串接 <see cref="CharacterController"/>。
/// </summary>
public sealed class VRPlayerRig : Component
{
	[Property, Group( "Features" )]
	public bool EnableLocomotion { get; set; } = true;

	[Property, Group( "Features" )]
	public bool EnableDesktopFallback { get; set; } = true;

	[Property, Group( "Features" )]
	public bool EnableLeftGrab { get; set; } = true;

	[Property, Group( "Features" )]
	public bool EnableRightGrab { get; set; } = true;

	[Property, Group( "Auto wire" ), Description( "為同物件上的 VRPlayerController 填入 CharacterController（若尚未指定）。" )]
	public bool AutoWireCharacterController { get; set; } = true;

	protected override void OnAwake()
	{
		ApplyFeatureToggles();
	}

	/// <summary>與 Inspector 相同的一次同步；執行時期可呼叫以切換功能。</summary>
	public void ApplyFeatureToggles()
	{
		CharacterController cc = null;
		if ( AutoWireCharacterController )
			cc = Components.Get<CharacterController>();

		var locomotion = Components.Get<VRPlayerController>();
		if ( locomotion is not null )
		{
			locomotion.Enabled = EnableLocomotion;
			if ( EnableLocomotion && cc is not null && locomotion.Controller is null )
				locomotion.Controller = cc;
		}

		var fallback = Components.Get<VRFallbackSimulator>();
		if ( fallback is not null )
			fallback.Enabled = EnableDesktopFallback;

		foreach ( var grabber in Components.GetAll<VRGrabber>( FindMode.EnabledInSelfAndDescendants ) )
		{
			grabber.Enabled = grabber.IsLeftHand ? EnableLeftGrab : EnableRightGrab;
		}
	}
}
