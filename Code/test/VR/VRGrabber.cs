using Sandbox;

/// <summary>
/// VR 手部抓取：依 Grip 建立 <see cref="FixedJoint"/>，並可選用 SkinnedModelRenderer 的 attachment 對齊物體。
/// </summary>
/// <remarks>
/// 編輯器設定檢核：
/// <list type="bullet">
/// <item>將此元件掛在左手或右手對應的 GameObject，並以 <see cref="IsLeftHand"/> 區分左右。</item>
/// <item>同一隻手（或子階層）需有 <b>Trigger</b> 的 Collider，才會收到 <see cref="Component.ITriggerListener"/> 事件。</item>
/// <item><see cref="HandRenderer"/> 請指定含 <see cref="SkinnedModelRenderer"/> 的物件；<see cref="AttachmentName"/> 須與 ModelDoc attachment 名稱完全一致（含大小寫）。</item>
/// <item>可抓物需具備 <see cref="Rigidbody"/>：優先使用同物件上的 <see cref="Grabbable"/>（可 Inspector 注入或啟用時快取），否則先找本體剛體，再以 <see cref="FindMode.EnabledInSelfAndDescendants"/> 保底。</item>
/// <item>關節建立在掛載此元件的 GameObject 上，該物件通常也需具備適當物理本體，關節行為才穩定。</item>
/// </list>
/// </remarks>
public sealed class VRGrabber : Component, Component.ITriggerListener
{
    [Property, Group( "VR 設定" ), Description( "勾選表示此元件掛在左手；未勾選表示右手。" )]
    public bool IsLeftHand { get; set; } = false;

    [Property, Group( "吸附設定" ), Description( "手部模型上的 SkinnedModelRenderer，用來讀取 ModelDoc attachment。" )]
    public SkinnedModelRenderer HandRenderer { get; set; }

    [Property, Group( "吸附設定" ), Description( "ModelDoc 中 attachment 名稱（例如 weapon_hold），須與模型完全一致。" )]
    public string AttachmentName { get; set; } = "weapon_hold";

    private GameObject _touchingObject;
    private GameObject _heldObject;
    private FixedJoint _grabJoint;

    protected override void OnUpdate()
    {
        var hand = IsLeftHand ? Input.VR.LeftHand : Input.VR.RightHand;

        if ( hand.Grip.Value > 0.5f && _heldObject == null && _touchingObject != null )
        {
            GrabObject( _touchingObject );
        }
        else if ( hand.Grip.Value < 0.2f && _heldObject != null )
        {
            ReleaseObject();
        }
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

    void GrabObject( GameObject obj )
    {
        if ( obj == null ) return;

        _heldObject = obj;

        if ( HandRenderer != null && !string.IsNullOrEmpty( AttachmentName ) )
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

    void ReleaseObject()
    {
        var released = _heldObject;

        if ( _grabJoint != null )
        {
            _grabJoint.Destroy();
        }

        if ( released != null && TryResolveRigidbody( released, out var rb ) )
        {
            var hand = IsLeftHand ? Input.VR.LeftHand : Input.VR.RightHand;
            // 手部角速度為 Angles；Rigidbody 需 Vector3，此處只傳遞線速度，角速度清零避免型別錯誤。
            rb.Velocity = hand.Velocity;
            rb.AngularVelocity = Vector3.Zero;
        }

        if ( released != null )
            VRSocket.NotifyGripReleased( Scene, released );

        _heldObject = null;
    }

    void ITriggerListener.OnTriggerEnter( Collider other )
    {
        if ( TryResolveRigidbody( other.GameObject, out _ ) )
        {
            _touchingObject = other.GameObject;
        }
    }

    void ITriggerListener.OnTriggerExit( Collider other )
    {
        if ( _touchingObject == other.GameObject )
        {
            _touchingObject = null;
        }
    }
}
