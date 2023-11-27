using static Editor.Inspectors.AssetInspector;

namespace Editor.Inspectors;

[CanEdit( "asset:object" )]
public class PrefabFileInspector : Widget, IAssetInspector
{
	Asset asset;

	public PrefabFileInspector( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();
	}

	PrefabFile resource;

	public void SetAsset( Asset asset )
	{
		this.asset = asset;
		resource = this.asset.LoadResource<PrefabFile>();
		Rebuild();
	}

	void Rebuild()
	{
		SerializedObject so = TypeLibrary.GetSerializedObject( resource );

		Layout.Clear( true );

		var cs = new ControlSheet();

		cs.AddRow( so.GetProperty( nameof( PrefabFile.ShowInMenu ) ) );
		cs.AddRow( so.GetProperty( nameof( PrefabFile.MenuPath ) ) );
		cs.AddRow( so.GetProperty( nameof( PrefabFile.MenuIcon ) ) );

		Layout.Add( cs );
	}

	public override void ChildValuesChanged( Widget source )
	{
		BindSystem.Flush();

		asset.SaveToDisk( resource );
	}
}
