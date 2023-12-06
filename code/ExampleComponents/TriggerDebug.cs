using Sandbox;

public sealed class TriggerDebug : Component, Component.ITriggerListener
{
	[Property] public NameTagPanel NameTag { get; set; }

	int iTouching;

	void ITriggerListener.OnTriggerEnter( Collider other ) 
	{
		iTouching++;

		NameTag.Name = $"{iTouching} touching\n{other.GameObject.Name} entered";
	}

	void ITriggerListener.OnTriggerExit( Collider other ) 
	{
		iTouching--;

		NameTag.Name = $"{iTouching} touching\n{other.GameObject.Name} left";
	}

}
