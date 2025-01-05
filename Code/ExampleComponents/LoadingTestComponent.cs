using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Internal;
using Sandbox.Services;
using System;
using System.Threading;

public sealed class LoadingTestComponent : Component
{
	protected override async Task OnLoad()
	{
		Log.Info( "Loading.." );

		LoadingScreen.Title = "Loading Test Component..";
		await Task.DelayRealtimeSeconds( 1.0f );

		LoadingScreen.Title = "Irradiating Testicles..";
		await Task.DelayRealtimeSeconds( 1.0f );

		for( int i=0; i<=100; i++ )
		{
			LoadingScreen.Title = $"{i} / 100";
			await Task.DelayRealtimeSeconds( 0.01f );
		}


		Log.Info( "Loading finished!" );
		await Task.DelayRealtimeSeconds( 1.0f );
	}

}
