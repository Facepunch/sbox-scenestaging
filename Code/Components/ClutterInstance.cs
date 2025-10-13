namespace Sandbox;

public struct ClutterInstance
{
	public Transform transform;

	public GameObject gameObject = null;
	public Model model = null;

	// For deferred prefab instantiation
	public PrefabFile prefab = null;
	public GameObject parent = null;

	public enum Type
	{
		Prefab, Model
	}
	public Type ClutterType { get; private set; }
	public Guid InstanceId { get; private set; }
	public float Size { get; private set; }
	public bool IsSmall { get; set; }

	public ClutterInstance( GameObject go, Transform t, bool isSmall = false )
		: this( t, isSmall )
	{
		gameObject = go;
		ClutterType = Type.Prefab;
		Size = go.GetBounds().Size.Length;
	}

	public ClutterInstance( Model m, Transform t, bool isSmall = false )
		: this( t, isSmall )
	{
		model = m;
		ClutterType = Type.Model;
		Size = m?.Bounds != null
			? m.Bounds.Size.Length * t.Scale.Length
			: t.Scale.Length;
	}

	/// <summary>
	/// Constructor for deferred prefab instantiation (created on worker threads, instantiated on main thread)
	/// </summary>
	public ClutterInstance( PrefabFile prefabFile, Transform t, bool isSmall = false, GameObject parentObject = null )
		: this( t, isSmall )
	{
		prefab = prefabFile;
		parent = parentObject;
		ClutterType = Type.Prefab;
		Size = 50f; // Default size, will be updated after instantiation
	}

	private ClutterInstance( Transform t, bool isSmall )
	{
		InstanceId = Guid.NewGuid();
		transform = t;
		IsSmall = isSmall;
		gameObject = null;
		model = null;
		prefab = null;
		parent = null;
		ClutterType = Type.Prefab;
		Size = 0f;
	}

	/// <summary>
	/// Instantiates the prefab if this is a deferred instance. Must be called on the main thread.
	/// </summary>
	public void InstantiatePrefab()
	{
		if ( prefab != null && gameObject == null )
		{
			gameObject = SceneUtility.GetPrefabScene( prefab ).Clone( transform );

			// Parent the GameObject if a parent is provided
			if ( parent != null )
			{
				gameObject.SetParent( parent, true );
			}

			// Tag for identification
			gameObject.Tags.Add( "scattered" );

			// Update size from actual bounds
			Size = gameObject.GetBounds().Size.Length;

			// Clear prefab reference
			prefab = null;
			parent = null;
		}
	}
}
