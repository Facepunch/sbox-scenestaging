using Sandbox;
using Sandbox.Utility;
using System.Linq;

public partial class Scene : GameObject
{
	List<Task> loadingTasks = new List<Task>();
	bool loading;

	internal void AddLoadingTask( Task loadingTask )
	{
		loadingTasks.Add( loadingTask );
	}

	public void StartLoading()
	{
		loading = true;
	}

	/// <summary>
	/// Return true if we're in an initial loading phase
	/// </summary>
	public bool IsLoading
	{
		get
		{
			loadingTasks.RemoveAll( x => x.IsCompleted );

			if ( !loading ) return false;
			if ( loadingTasks.Any() ) return true;

			loading = false;
			return false;
		}
	}
}
