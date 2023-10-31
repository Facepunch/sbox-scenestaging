using Sandbox;

[Title( "Anchor" )]
[Category( "VR" )]
[Icon( "anchor" )]
public class AnchorComponent : BaseComponent
{
	[Flags]
	public enum UpdateTypes
	{
		None,
		OnPreRender,
		Update,

		All = OnPreRender | Update
	}

	[Property]
	public UpdateTypes UpdateType { get; set; }

	/// <summary>
	/// Update <see cref="VR.Anchor"/> based on the gameobject's transform
	/// </summary>
	private void UpdateAnchor()
	{
		VR.Anchor = GameObject.Transform.World;
	}

	public override void Update()
	{
		if ( !Enabled || Scene.IsEditor )
			return;

		if ( UpdateType.HasFlag( UpdateTypes.Update ) )
		{
			UpdateAnchor();
		}
	}

	protected override void OnPreRender()
	{
		if ( !Enabled || Scene.IsEditor )
			return;

		if ( UpdateType.HasFlag( UpdateTypes.OnPreRender ) )
		{
			UpdateAnchor();
		}
	}
}
