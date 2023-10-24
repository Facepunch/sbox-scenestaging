using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Services;
using System;
using System.Threading;

public sealed class AlphaOverTime : BaseComponent
{
	[Property] float Time { get; set; } = 5.0f;
	[Property] Curve Alpha { get; set; }

	TimeSince timeSinceEnabled;

	public override void OnEnabled()
	{
		base.OnEnabled();

		timeSinceEnabled = 0;
	}

	public override void Update()
	{
		base.Update();

		var a = Alpha.EvaluateDelta( timeSinceEnabled / Time );

		foreach ( var component in  GetComponents<ModelComponent>( true, true ) )
		{
			component.Tint = component.Tint.WithAlpha( a );
		}
			
	}

}
