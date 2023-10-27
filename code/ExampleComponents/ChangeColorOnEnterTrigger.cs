using Sandbox;
using System;

public sealed class ChangeColorOnEnterTrigger : BaseComponent, BaseComponent.ITriggerListener
{
	[Property]
	public Gradient ColorRange { get; set; }

	void ITriggerListener.OnTriggerEnter( Collider other ) 
	{
		foreach( var c in GetComponents<ModelComponent>( true, true ) )
		{
			c.Tint = ColorRange.Evaluate( Random.Shared.Float() );
		}
	}

	void ITriggerListener.OnTriggerExit( Collider other ) 
	{
		foreach ( var c in GetComponents<ModelComponent>( true, true ) )
		{
			c.Tint = Color.White;
		}
	}

}
