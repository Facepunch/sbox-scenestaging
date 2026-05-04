using Sandbox;

/// <summary>
/// 幽靈目標：無剛體、無碰撞，每幀將自身世界 Transform 對齊「真實手把／握持點」，供日後彈簧關節等追蹤。
/// </summary>
/// <remarks>
/// 預設以 <see cref="TransformSource"/>（通常為已掛 <see cref="Sandbox.VR.VRTrackedObject"/> 或由 <see cref="VRFallbackSimulator"/> 驅動的手根）為來源，桌面與真 VR 一致。
/// 若 <see cref="UseVrInputDirect"/> 且 <see cref="Game.IsRunningInVR"/>，則改讀 <see cref="Input.VR.LeftHand"/>／<see cref="Input.VR.RightHand"/> 的 <c>Transform</c>（grip pose，世界空間）。
/// 若指定 <see cref="HandRenderer"/> 與 <see cref="AttachmentName"/> 且 attachment 存在，則對齊該點（與 <see cref="VRGrabber"/> 吸附邏輯一致）。
/// 日後若與物理關節併用出現抖動，可改 <see cref="SyncInFixedUpdate"/> 為真，或改以 Keyframed <c>PhysicsBody</c> 在 Fixed 步驟中跟隨目標。
/// </remarks>
public sealed class VRGhostHandTarget : Component
{
	[Property, Group( "VR" ), Description( "除錯標籤用；與 UseVrInputDirect 搭配以選擇左／右手 Input.VR。" )]
	public bool IsLeftHand { get; set; }

	[Property, Group( "Source" ), Description( "非 VR 或關閉直接讀 Input 時使用；建議指向 VRTrackedObject 手根（與 Fallback 相容）。" )]
	public GameObject TransformSource { get; set; }

	[Property, Group( "Source" ), Description( "為真且 Game.IsRunningInVR 時，以 Input.VR 左右手 Transform（grip）覆寫來源。" )]
	public bool UseVrInputDirect { get; set; }

	[Property, Group( "Source" ), Description( "為真時使用 AimTransform（瞄準姿態）而非 grip Transform。" )]
	public bool UseAimPose { get; set; }

	[Property, Group( "Attachment" ), Description( "若設定且 attachment 存在，幽靈對齊該點世界座標（與 VRGrabber 一致）。" )]
	public SkinnedModelRenderer HandRenderer { get; set; }

	[Property, Group( "Attachment" )]
	public string AttachmentName { get; set; } = "weapon_hold";

	[Property, Group( "Sync" ), Description( "為真時在 OnFixedUpdate 寫入 Transform，較利於與物理步長對齊。" )]
	public bool SyncInFixedUpdate { get; set; }

	[Property, Group( "Debug" )]
	public bool ShowDebugGizmo { get; set; }

	protected override void OnUpdate()
	{
		if ( !SyncInFixedUpdate )
			ApplyGhostTransform();
	}

	protected override void OnFixedUpdate()
	{
		if ( SyncInFixedUpdate )
			ApplyGhostTransform();
	}

	void ApplyGhostTransform()
	{
		Transform? worldTx = null;

		if ( HandRenderer.IsValid() && !string.IsNullOrEmpty( AttachmentName ) )
		{
			var att = HandRenderer.GetAttachment( AttachmentName );
			if ( att.HasValue )
			{
				worldTx = new Transform( att.Value.Position, att.Value.Rotation );
			}
		}

		if ( !worldTx.HasValue )
			worldTx = ResolveBaseWorldTransform();

		if ( !worldTx.HasValue )
			return;

		WorldTransform = worldTx.Value;
	}

	Transform? ResolveBaseWorldTransform()
	{
		if ( UseVrInputDirect && Game.IsRunningInVR )
		{
			var ctl = IsLeftHand ? Input.VR.LeftHand : Input.VR.RightHand;
			return UseAimPose ? ctl.AimTransform : ctl.Transform;
		}

		if ( TransformSource.IsValid() )
			return TransformSource.WorldTransform;

		return null;
	}

	protected override void DrawGizmos()
	{
		if ( !ShowDebugGizmo )
			return;

		Gizmo.Draw.Color = IsLeftHand ? Color.Cyan : Color.Magenta;
		Gizmo.Draw.LineSphere( WorldPosition, 2.5f );
	}
}
