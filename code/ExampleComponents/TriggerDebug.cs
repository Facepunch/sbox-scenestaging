using Sandbox;

public sealed class TriggerDebug : BaseComponent, BaseComponent.ITriggerListener
{
	[Property] public NameTagPanel NameTag { get; set; }

	int iTouching;

	void ITriggerListener.OnTriggerEnter( ColliderBaseComponent other ) 
	{
		Log.Info( $"{other} entered a trigger" );

		// get our trigger
		iTouching++;

		NameTag.Name = $"{iTouching} touching\n{other.GameObject.Name} entered";
	}

	void ITriggerListener.OnTriggerExit( ColliderBaseComponent other ) 
	{
		iTouching--;

		NameTag.Name = $"{iTouching} touching\n{other.GameObject.Name} left";
	}

}
