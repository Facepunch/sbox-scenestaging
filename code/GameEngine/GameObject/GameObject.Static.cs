public partial class GameObject
{
	/// <summary>
	/// Create a GameObject on the active scene
	/// </summary>
	public static GameObject Create( bool enabled = true, string name = "GameObject" )
	{
		if ( GameManager.ActiveScene is null )
			throw new System.ArgumentNullException( "Trying to create a GameObject without an active scene" );

		return new GameObject( enabled, name );
	}
}
