using Sandbox;

/// <summary>
/// VR 插槽：以 Trigger 偵測候選物，並在放開 Grip（或低速進入）時將帶有 <see cref="Socketable"/> 的物件鎖到插槽。
/// </summary>
public sealed class VRSocket : Component, Component.ITriggerListener
{
    public enum SocketLockMode
    {
        ParentToSocket,
        FixedJointToSocketBody
    }

    [Property, Group( "Socket" ), Description( "只接受 Socketable.SocketId 與此字串相同的物件；留空表示接受任意 Socketable。" )]
    public string AcceptId { get; set; } = "";

    [Property, Group( "Socket" ), Description( "吸附後對齊的世界 Transform；留空則使用本物件 WorldTransform。" )]
    public GameObject SlotAnchor { get; set; }

    [Property, Group( "Socket" )]
    public float SnapRadius { get; set; } = 24.0f;

    [Property, Group( "Socket" ), Description( "槽內已有物體時拒絕再插入。" )]
    public bool RequireEmpty { get; set; } = true;

    [Property, Group( "Snap" ), Description( "放手後若物體在插槽半徑內則嘗試吸附（與 VRGrabber 銜接）。" )]
    public bool SnapOnGripRelease { get; set; } = true;

    [Property, Group( "Snap" ), Description( "候選物在 Trigger 內且線速度低於門檻時自動吸附。" )]
    public bool AutoSnapWhenSlowInTrigger { get; set; } = false;

    [Property, Group( "Snap" )]
    public float MaxSettleSpeed { get; set; } = 32.0f;

    [Property, Group( "Lock" )]
    public SocketLockMode LockMode { get; set; } = SocketLockMode.ParentToSocket;

    [Property, Group( "Lock" ), Description( "LockMode 為 FixedJointToSocketBody 時使用；應為插槽側帶有 Rigidbody 的物件。" )]
    public Rigidbody SocketJointBody { get; set; }

    GameObject _hoverCandidate;
    GameObject _occupiedItem;
    FixedJoint _occupyingJoint;

    Transform GetSlotWorldTransform()
    {
        if ( SlotAnchor.IsValid() )
            return SlotAnchor.WorldTransform;
        return GameObject.WorldTransform;
    }

    /// <summary>由 <see cref="VRGrabber"/> 放手後呼叫，讓各插槽嘗試吸附。</summary>
    public static void NotifyGripReleased( Scene scene, GameObject releasedItem )
    {
        if ( scene == null || !releasedItem.IsValid() )
            return;

        foreach ( var socket in scene.GetAllComponents<VRSocket>() )
        {
            if ( !socket.SnapOnGripRelease )
                continue;
            socket.TrySnapAfterGripRelease( releasedItem );
        }
    }

    bool IdsMatch( Socketable socketable )
    {
        if ( string.IsNullOrEmpty( AcceptId ) )
            return true;
        return string.Equals( socketable.SocketId, AcceptId, System.StringComparison.Ordinal );
    }

    bool DistanceAllowsSnap( GameObject item, Socketable socketable )
    {
        var slotPos = GetSlotWorldTransform().Position;
        var attach = socketable.GetAttachWorldPosition();
        return (attach - slotPos).Length <= SnapRadius;
    }

    void TrySnapAfterGripRelease( GameObject item )
    {
        if ( !item.IsValid() )
            return;
        if ( RequireEmpty && _occupiedItem.IsValid() )
            return;

        var socketable = item.Components.GetInAncestorsOrSelf<Socketable>();
        if ( !socketable.IsValid() )
            return;
        if ( !IdsMatch( socketable ) )
            return;
        if ( !DistanceAllowsSnap( item, socketable ) )
            return;

        TrySnapInternal( item, socketable );
    }

    void ITriggerListener.OnTriggerEnter( Collider other )
    {
        var socketable = other.GameObject.Components.GetInAncestorsOrSelf<Socketable>();
        if ( !socketable.IsValid() )
            return;
        if ( !VRGrabber.TryResolveRigidbody( other.GameObject, out var rb ) || !rb.IsValid() )
            return;

        _hoverCandidate = socketable.GameObject;
    }

    void ITriggerListener.OnTriggerExit( Collider other )
    {
        var socketable = other.GameObject.Components.GetInAncestorsOrSelf<Socketable>();
        if ( !socketable.IsValid() )
            return;
        if ( _hoverCandidate == socketable.GameObject )
            _hoverCandidate = null;
    }

    protected override void OnFixedUpdate()
    {
        if ( !AutoSnapWhenSlowInTrigger )
            return;
        if ( !_hoverCandidate.IsValid() )
            return;
        if ( RequireEmpty && _occupiedItem.IsValid() )
            return;

        var socketable = _hoverCandidate.Components.GetInAncestorsOrSelf<Socketable>();
        if ( !socketable.IsValid() )
            return;
        if ( !IdsMatch( socketable ) )
            return;
        if ( !VRGrabber.TryResolveRigidbody( _hoverCandidate, out var rb ) || !rb.IsValid() )
            return;
        if ( rb.Velocity.Length > MaxSettleSpeed )
            return;
        if ( !DistanceAllowsSnap( _hoverCandidate, socketable ) )
            return;

        TrySnapInternal( _hoverCandidate, socketable );
    }

    void TrySnapInternal( GameObject item, Socketable socketable )
    {
        if ( !item.IsValid() )
            return;
        if ( RequireEmpty && _occupiedItem.IsValid() )
            return;

        var slotTx = GetSlotWorldTransform();
        var attach = socketable.GetAttachWorldPosition();
        var delta = slotTx.Position - attach;
        item.WorldPosition += delta;
        item.WorldRotation = slotTx.Rotation;

        VRGrabber.TryResolveRigidbody( item, out var rb );
        if ( rb.IsValid() )
        {
            rb.Velocity = Vector3.Zero;
            rb.AngularVelocity = Vector3.Zero;
        }

        if ( LockMode == SocketLockMode.FixedJointToSocketBody && SocketJointBody.IsValid() )
        {
            _occupyingJoint?.Destroy();
            _occupyingJoint = SocketJointBody.GameObject.Components.Create<FixedJoint>();
            _occupyingJoint.Body = item;
            if ( rb.IsValid() )
                rb.Enabled = false;
        }
        else
        {
            item.SetParent( GameObject, true );
            if ( rb.IsValid() )
                rb.Enabled = false;
        }

        _occupiedItem = item;
        socketable.NotifySocketed( this );
    }

    /// <summary>將目前鎖在槽內的物件解鎖（可綁到其他輸入或按鈕）。</summary>
    public void Unsnap()
    {
        if ( !_occupiedItem.IsValid() )
            return;

        var socketable = _occupiedItem.Components.GetInAncestorsOrSelf<Socketable>();
        if ( socketable.IsValid() )
            socketable.NotifyUnsocketed();

        if ( _occupyingJoint != null )
        {
            _occupyingJoint.Destroy();
            _occupyingJoint = null;
        }
        else if ( _occupiedItem.Parent == GameObject )
        {
            _occupiedItem.SetParent( null, true );
        }

        if ( VRGrabber.TryResolveRigidbody( _occupiedItem, out var rb ) && rb.IsValid() )
            rb.Enabled = true;

        _occupiedItem = null;
    }
}
