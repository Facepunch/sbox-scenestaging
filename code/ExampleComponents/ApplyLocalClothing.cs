namespace Sandbox;

public sealed class ApplyLocalClothing : Component
{
	[Property] public SkinnedModelRenderer BodyTarget { get; set; }

	protected override void OnAwake()
	{
		var clothing = ClothingContainer.CreateFromLocalUser();
		clothing.Apply( BodyTarget );
	}
}
