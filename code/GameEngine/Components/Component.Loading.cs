using Sandbox;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public partial class Component
{
	protected virtual Task OnLoad()
	{
		return Task.CompletedTask;
	}

	private void LaunchLoader()
	{
		var loadingTask = OnLoad();
		if ( loadingTask.IsCompletedSuccessfully ) return;

		Scene.AddLoadingTask( loadingTask );
	}

	internal void OnLoadInternal()
	{
		CallbackBatch.Add( CommonCallback.Loading, LaunchLoader, this, "LaunchLoader" );
	}
}
