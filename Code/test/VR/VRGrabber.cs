using Sandbox;
using VRLogic;

/// <summary>
/// VR 手部抓取（Interactor）：Hover 由 Trigger 驅動；Attach／Release 在 <see cref="OnFixedUpdate"/> 執行以對齊物理步。
/// 可選用 <see cref="SkinnedModelRenderer.GetAttachment"/> 對齊 ModelDoc attachment，再建立 <see cref="FixedJoint"/>。
/// </summary>
/// <remarks>
/// 編輯器設定檢核：見原類別註解；<see cref="AttachmentName"/> 預設與 <see cref="VrInteractionConstants.DefaultGripAttachmentName"/> 一致。
/// </remarks>
public sealed class VRGrabber : Component, Component.ITriggerListener
{
	[Property, Group( "VR 設定" ), Description( "勾選表示此元件掛在左手；未勾選表示右手。" )]
	public bool IsLeftHand { get; set; }

	[Property, Group( "吸附設定" )]
	public SkinnedModelRenderer HandRenderer { get; set; }

	[Property, Group( "吸附設定" ), Description( "ModelDoc attachment 名稱；須與模型完全一致（含大小寫）。" )]
	public string AttachmentName { get; set; } = VrInteractionConstants.DefaultGripAttachmentName;

	[Property, Group( "輸入" )]
	public float GripPressThreshold { get; set; } = VrInteractionConstants.DefaultGripPressThreshold;

	[Property, Group( "輸入" )]
	public float GripReleaseThreshold { get; set; } = VrInteractionConstants.DefaultGripReleaseThreshold;

	/// <summary>Idle：無候選；Hovering：Trigger 內有物；Holding：已建立關節。</summary>
	public GrabInteractorState State { get; private set; } = GrabInteractorState.Idle;

	GameObject _touchingObject;
	GameObject _heldObject;
	FixedJoint _grabJoint;

	GameObject _pendingGrabTarget;
	bool _pendingRelease;
	Vector3 _releaseLinearVelocity;

	protected override void OnUpdate()
	{
		var hand = IsLeftHand ? Input.VR.LeftHand : Input.VR.RightHand;
		var grip = hand.Grip.Value;

		if ( GrabInteractionRules.ShouldStartGrab( grip, GripPressThreshold, _heldObject is not null, _touchingObject is not null ) )
			_pendingGrabTarget = _touchingObject;

		if ( GrabInteractionRules.ShouldReleaseGrab( grip, GripReleaseThreshold, _heldObject is not null ) )
		{
			_pendingRelease = true;
			_releaseLinearVelocity = hand.Velocity;
		}

		UpdatePresentationState();
	}

	protected override void OnFixedUpdate()
	{
		if ( _pendingRelease )
		{
			_pendingGrabTarget = null;
			_pendingRelease = false;
			ReleaseObjectInternal();
		}

		if ( _pendingGrabTarget is not null && _heldObject is null )
		{
			var hand = IsLeftHand ? Input.VR.LeftHand : Input.VR.RightHand;
			if ( hand.Grip.Value < GripPressThreshold )
			{
				_pendingGrabTarget = null;
				return;
			}

			var target = _pendingGrabTarget;
			_pendingGrabTarget = null;
			GrabObjectInternal( target );
		}
	}

	void UpdatePresentationState()
	{
		if ( _heldObject is not null )
			State = GrabInteractorState.Holding;
		else if ( _touchingObject is not null )
			State = GrabInteractorState.Hovering;
		else
			State = GrabInteractorState.Idle;
	}

	public static bool TryResolveRigidbody( GameObject root, out Rigidbody rb )
	{
		if ( root.Components.TryGet<Grabbable>( out var grabbable ) && grabbable.TryGetBody( out rb ) )
			return true;

		if ( root.Components.TryGet<Rigidbody>( out rb ) && rb.IsValid() )
			return true;

		rb = root.Components.Get<Rigidbody>( FindMode.EnabledInSelfAndDescendants );
		return rb.IsValid();
	}

	void GrabObjectInternal( GameObject obj )
	{
		if ( obj is null || !obj.IsValid() )
			return;

		_heldObject = obj;

		if ( HandRenderer is not null && !string.IsNullOrEmpty( AttachmentName ) )
		{
			var attachmentTx = HandRenderer.GetAttachment( AttachmentName );

			if ( attachmentTx.HasValue )
			{
				obj.Transform.Position = attachmentTx.Value.Position;
				obj.Transform.Rotation = attachmentTx.Value.Rotation;

				if ( TryResolveRigidbody( obj, out var rb ) )
				{
					rb.Velocity = Vector3.Zero;
					rb.AngularVelocity = Vector3.Zero;
				}
			}
		}

		_grabJoint = Components.Create<FixedJoint>();
		_grabJoint.Body = obj;
	}

	void ReleaseObjectInternal()
	{
		var released = _heldObject;

		_grabJoint?.Destroy();
		_grabJoint = null;

		if ( released is not null && released.IsValid() && TryResolveRigidbody( released, out var rb ) )
		{
			rb.Velocity = AlyxFeelTuningDefaults.PreferHandLinearVelocityOnRelease ? _releaseLinearVelocity : rb.Velocity;
			rb.AngularVelocity = Vector3.Zero;
		}

		if ( released is not null && released.IsValid() )
			GripReleaseNotification.Publish?.Invoke( Scene, released );

		_heldObject = null;
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( TryResolveRigidbody( other.GameObject, out _ ) )
			_touchingObject = other.GameObject;
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		if ( _touchingObject == other.GameObject )
			_touchingObject = null;
	}
}

/// <summary>VR 抓取 Interactor 高階狀態（細節仍以物理與 Trigger 為準）。</summary>
public enum GrabInteractorState
{
	Idle,
	Hovering,
	Holding
}
