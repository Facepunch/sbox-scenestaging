public partial class GameObject
{
	string PrefabSource { get; set; }

	public void SetPrefabSource( string prefabSource )
	{
		PrefabSource = prefabSource;
	}

	/// <summary>
	/// We are instantiated from a prefab. Stop that.
	/// </summary>
	public void BreakFromPrefab()
	{
		if ( PrefabSource is null )
			return;

		PrefabSource = null;
		EditLog( "Break From Prefab", this );
	}

	public void UpdateFromPrefab()
	{
		//
		// This routine is the slowest possible way to do it right now
		// what we need is some kind of "sync" routine for both GameObjects
		// and Components.. to avoid deleting and recreating every object and
		// component (which is obviously recreating every physics object and component)
		//

		var s = Serialize();


		Clear();
		Deserialize( s );
	}

	public string PrefabInstanceSource
	{
		get
		{
			if ( PrefabSource is not null ) return PrefabSource;
			if ( Parent is null ) return default;
			return Parent.PrefabInstanceSource;
		}
	}

	public bool IsPrefabInstance
	{
		get
		{
			if ( IsPrefabInstanceRoot ) return true;
			if ( Parent is null ) return false;

			return Parent.IsPrefabInstance;
		}
	}

	public bool IsPrefabInstanceRoot
	{
		get
		{
			if ( PrefabSource is not null ) return true;
			return false;
		}
	}
}
