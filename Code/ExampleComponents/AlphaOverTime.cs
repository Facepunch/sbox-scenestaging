using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Services;
using System;
using System.Threading;

public sealed class AlphaOverTime : Component
{
	[Property] float Time { get; set; } = 5.0f;
	[Property] Curve Alpha { get; set; }

	TimeSince timeSinceEnabled;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		timeSinceEnabled = 0;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		var a = Alpha.EvaluateDelta( timeSinceEnabled / Time );

		foreach ( var component in Components.GetAll<ModelRenderer>( FindMode.EnabledInSelfAndDescendants ) )
		{
			component.Tint = component.Tint.WithAlpha( a );
		}
			
	}

}
