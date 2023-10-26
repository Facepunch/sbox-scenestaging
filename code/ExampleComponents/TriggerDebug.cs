using Sandbox;

public sealed class TriggerDebug : BaseComponent
{
	[Property] public NameTagPanel NameTag { get; set; } 

	public override void Update()
	{
		var c = GetComponent<ColliderBaseComponent>();
		if ( c == null ) return;

		NameTag.Name = $"{c.Touching.Count} Touching";
	}
}
