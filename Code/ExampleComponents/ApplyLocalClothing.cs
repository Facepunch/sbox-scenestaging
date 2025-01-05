namespace Sandbox;

[Alias( "Sandbox.ApplyLocalClothing" )]
public sealed class Dresser : Component, Component.ExecuteInEditor
{
	[Property]
	public SkinnedModelRenderer BodyTarget { get; set; }

	[Property]
	public bool ApplyLocalUserClothes { get; set; } = true;

	[Property]
	public bool ApplyHeightScale { get; set; } = true;

	[Property]
	public List<ClothingContainer.ClothingEntry> Clothing { get; set; }

	protected override void OnAwake()
	{
		if ( IsProxy )
			return;

		Apply();
	}

	void Apply()
	{
		if ( !BodyTarget.IsValid() )
			return;

		var clothing = ApplyLocalUserClothes ? ClothingContainer.CreateFromLocalUser() : new ClothingContainer();

		if ( !ApplyHeightScale )
			clothing.Height = 1;

		clothing.AddRange( Clothing );
		clothing.Normalize();

		clothing.Apply( BodyTarget );

		BodyTarget.PostAnimationUpdate();
	}

	protected override void OnValidate()
	{
		if ( IsProxy )
			return;

		base.OnValidate();

		using var p = Scene.Push();

		if ( !BodyTarget.IsValid() )
		{
			BodyTarget = GetComponentInChildren<SkinnedModelRenderer>();
		}

		Apply();
	}
}
