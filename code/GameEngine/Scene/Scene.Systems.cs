public partial class Scene
{
	void UpdateAnimationThreaded()
	{
		if ( !ThreadedAnimation )
			return;

		// TODO - faster way to accumulate these
		var animModel = GetComponents<SkinnedModelRenderer>( true, true ).ToArray();

		//
		// Run the updates and the bone merges in a thread
		//
		using ( Sandbox.Utility.Superluminal.Scope( "Scene.AnimUpdate", Color.Cyan ) )
		{
			Sandbox.Utility.Parallel.ForEach( animModel, x => x.UpdateInThread() );
		}

		//
		// Run events in the main thread
		//
		using ( Sandbox.Utility.Superluminal.Scope( "Scene.AnimPostUpdate", Color.Yellow ) )
		{
			foreach ( var x in animModel )
			{
				x.PostAnimationUpdate();
			}
		}
	}
}
