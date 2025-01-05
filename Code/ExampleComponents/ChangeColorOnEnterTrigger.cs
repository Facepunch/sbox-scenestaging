using Sandbox;
using System;

public sealed class ChangeColorOnEnterTrigger : Component, Component.ITriggerListener
{
	[Property]
	public Gradient ColorRange { get; set; }

	void ITriggerListener.OnTriggerEnter( Collider other ) 
	{
		foreach( var c in Components.GetAll<ModelRenderer>( FindMode.EnabledInSelfAndDescendants ) )
		{
			c.Tint = ColorRange.Evaluate( Random.Shared.Float() );
		}
	}

	void ITriggerListener.OnTriggerExit( Collider other ) 
	{
		foreach ( var c in Components.GetAll<ModelRenderer>( FindMode.EnabledInSelfAndDescendants ) )
		{
			c.Tint = Color.White;
		}
	}

}
