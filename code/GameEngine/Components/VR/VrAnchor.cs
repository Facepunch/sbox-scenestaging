using Sandbox;

/// <summary>
/// Updates the <see cref="VR.Anchor"/> based on a GameObject's transform.
/// </summary>
[Title( "VR Anchor" )]
[Category( "VR" )]
[Icon( "anchor" )]
public class VrAnchor : Component
{
	/// <summary>
	/// Update <see cref="VR.Anchor"/> based on the GameObject's transform
	/// </summary>
	private void UpdateAnchor()
	{
		VR.Anchor = GameObject.Transform.World;
	}

	protected override void OnUpdate()
	{
		if ( !Enabled || Scene.IsEditor )
			return;

		UpdateAnchor();
	}

	protected override void OnPreRender()
	{
		if ( !Enabled || Scene.IsEditor )
			return;

		UpdateAnchor();
	}
}
