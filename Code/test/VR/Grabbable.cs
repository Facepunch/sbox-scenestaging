using Sandbox;

/// <summary>
/// 標記可被抓取的物件，並可選擇明確指定或快取要使用的 <see cref="Rigidbody"/>，供 <see cref="VRGrabber"/> 等系統以混合解析讀取。
/// </summary>
public sealed class Grabbable : Component
{
    [Property, Description( "可選：手動指定抓取時要驅動的 Rigidbody；未指定則在本物件與子階層尋找一次並快取。" )]
    public Rigidbody OverrideBody { get; set; }

    private Rigidbody _resolved;

    protected override void OnAwake()
    {
        base.OnAwake();
        RefreshResolved();
    }

    protected override void OnEnabled()
    {
        base.OnEnabled();
        RefreshResolved();
    }

    void RefreshResolved()
    {
        if ( OverrideBody.IsValid() )
            _resolved = OverrideBody;
        else
            _resolved = Components.Get<Rigidbody>( FindMode.EnabledInSelfAndDescendants );
    }

    /// <summary>
    /// 取得 <see cref="OnAwake"/> / <see cref="OnEnabled"/> 時快取的剛體。
    /// </summary>
    public bool TryGetBody( out Rigidbody rb )
    {
        rb = _resolved;
        return rb.IsValid();
    }
}
